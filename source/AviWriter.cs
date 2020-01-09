using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Runtime.InteropServices; // for StructLayout

namespace GitHub.secile.Avi
{
    /// <summary>
    /// MotionJPEG形式のAVI動画を作成するクラス
    /// </summary>
    public class AviWriter
    {
        public Action<byte[]> AddImage { get; private set; }
        public Action<byte[]> AddAudio { get; private set; }
        public Action Close { get; private set; }

        public class VideoFormat
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public float FramesPerSec { get; set; }
        }

        public class AudioFormat
        {
            /// <summary>1 or 2.</summary>
            public short Channels { get; set; }

            /// <summary>8000, 11025, 22050, 44100, etc...</summary>
            public int SamplesPerSec { get; set; }

            /// <summary>8 or 16.</summary>
            public short BitsPerSample { get; set; }
        }

        /// <summary>Create with video stream.</summary>
        public AviWriter(System.IO.Stream outputAvi, string fourCC, int width, int height, float fps)
            : this(outputAvi, fourCC, new VideoFormat() { Width = width, Height = height, FramesPerSec = fps }) { }

        /// <summary>Create with video stream.</summary>
        public AviWriter(System.IO.Stream outputAvi, string fourCC, VideoFormat videoFormat)
            : this(outputAvi, fourCC, videoFormat, null) { }

        /// <summary>Create with video and audio stream.</summary>
        public AviWriter(System.IO.Stream outputAvi, string fourCC, VideoFormat videoFormat, AudioFormat audioFormat)
        {
            // RIFFファイルは、RIFFヘッダーとその後ろに続く 0個以上のリストとチャンクで構成されている。
            // RIFFヘッダーは、'RIFF'のFOURCC、4バイトのデータサイズ、データを識別するFOURCC、データから構成されている。
            // リストは、'LIST'のFOURCC、4バイトのデータサイズ、データを識別するFOURCC、データから構成されている。
            // チャンクは、データを識別するFOURCC、4バイトのデータサイズ、データから構成されている。
            // チャンクデータを識別するFOURCCは、2桁のストリーム番号とその後に続く2文字コード(dc=ビデオ，wb=音声，tx=字幕など)で構成されている。
            // AVIファイルは、'AVI 'のFOURCCと、2つの必須のLISTチャンク('hdrl''movi')、オプションのインデックスチャンクから構成されるRIFFファイルである。

            var riffFile = new RiffFile(outputAvi, "AVI ");

            // hdrlリストをとりあえずフレーム数=0で作成(あとで上書き)
            var hdrlList = riffFile.CreateList("hdrl");
            WriteHdrlList(hdrlList, fourCC, videoFormat, audioFormat, 0, 0);
            hdrlList.Close();

            // moviリストを作成し、AddImage/AddAudioごとにデータチャンクを追加
            var idx1List = new List<Idx1Entry>();
            var moviList = riffFile.CreateList("movi");
            
            this.AddImage += (data) =>
            {
                if (videoFormat == null) throw new InvalidOperationException("no video stream.");
                var idx1 = WriteMoviList(moviList, "00dc", data);
                idx1List.Add(idx1);
            };

            this.AddAudio += (data) =>
            {
                if (audioFormat == null) throw new InvalidOperationException("no audio stream.");
                var idx1 = WriteMoviList(moviList, "01wb", data);
                idx1List.Add(idx1);
            };

            // ファイルをクローズ
            this.Close += () =>
            {
                // moviリストを閉じる
                moviList.Close();

                // idx1チャンクを作成
                WriteIdx1Chunk(riffFile, idx1List);

                var videoFrames = idx1List.Where(x => x.ChunkId == "00dc").Count();
                var audioFrames = idx1List.Where(x => x.ChunkId == "01wb").Count();

                // hdrlListを正しいフレーム数で上書き
                var offset = hdrlList.Offset;
                riffFile.BaseStream.Seek(offset, System.IO.SeekOrigin.Begin); // hdrlリストの先頭まで戻る
                riffFile.BaseStream.Seek(12, System.IO.SeekOrigin.Current);   // hdrlリストのヘッダ分飛ばす
                WriteHdrlList(riffFile, fourCC, videoFormat, audioFormat, videoFrames, audioFrames);  // hdrlリストのデータを正しいフレーム数で上書き
                riffFile.BaseStream.Seek(0, System.IO.SeekOrigin.End);        // 元の場所に戻る

                // ファイルをクローズ
                riffFile.Close();
                outputAvi.Dispose();
            };
        }

        /// <summary>Create Hdrl</summary>
        private void WriteHdrlList(RiffList hdrlList, string fourCC, VideoFormat videoFormat, AudioFormat audioFormat, int videoFrames, int audioFrames)
        {
            // - xxxx.avi - RIFF - AVI
            //   |
            //   - LIST - hdrl         : Header List
            //   | |
            //   | |-- avih            : Main AVI Header 
            //   | |
            //   | |-- LIST - strl     : Stream Header List
            //   | |   |-- strh        : Stream Header
            //   | |   +-- strf        : BITMAPINFOHEADER
            //   | |
            //   | +-- LIST - strl     : Stream Header List
            //   |     |-- strh        : Stream Header
            //   |     +-- strf        : WAVEFORMATEX
            //   |
            //   - LIST - movi
            //   | |
            //   | |-- 00dc            : stream 0 Video Chunk
            //   | |-- 01wb            : stream 1 Audio Chunk
            //   | |-- 00dc
            //   | |-- 01wb
            //   | +-- ....
            //   |
            //   + idx1
            //
            // some avi files insert JUNK chunk before 'LIST - movi' to align 2048 bytes boundary.
            // but I cant find any reason to align 2048 bytes boundary.

            if (videoFormat == null) throw new ArgumentNullException("videoFormat");

            int streams = audioFormat == null ? 1 : 2;

            // LISTチャンク'hdrl'を追加
            // 'hdrl' リストは AVI メイン ヘッダーで始まり、このメイン ヘッダーは 'avih' チャンクに含まれている。
            // メイン ヘッダーには、ファイル内のストリーム数、AVI シーケンスの幅と高さなど、AVI ファイル全体に関するグローバル情報が含まれる。
            // メイン ヘッダー チャンクは、AVIMAINHEADER 構造体で構成されている。
            {
                var chunk = hdrlList.CreateChunk("avih");
                var avih = new AVIMAINHEADER();
                avih.dwMicroSecPerFrame = (uint)(1 / videoFormat.FramesPerSec * 1000 * 1000);
                avih.dwMaxBytesPerSec = 25000; // ffmpegと同じ値に
                avih.dwFlags = 0x0910;         // ffmpegと同じ値に
                avih.dwTotalFrames = (uint)videoFrames;
                avih.dwStreams = (uint)streams;
                avih.dwSuggestedBufferSize = 0x100000;
                avih.dwWidth = (uint)videoFormat.Width;
                avih.dwHeight = (uint)videoFormat.Height;

                var data = StructureToBytes(avih);
                chunk.Write(data);
                chunk.Close();
            }

            // メイン ヘッダーの次には、1 つ以上の 'strl' リストが続く。'strl' リストは各データ ストリームごとに必要である。
            // 各 'strl' リストには、ファイル内の単一のストリームに関する情報が含まれ、ストリーム ヘッダー チャンク ('strh') とストリーム フォーマット チャンク ('strf') が必ず含まれる。
            // ストリーム ヘッダー チャンク ('strh') は、AVISTREAMHEADER 構造体で構成されている。
            // ストリーム フォーマット チャンク ('strf') は、ストリーム ヘッダー チャンクの後に続けて記述する必要がある。
            // ストリーム フォーマット チャンクは、ストリーム内のデータのフォーマットを記述する。このチャンクに含まれるデータは、ストリーム タイプによって異なる。
            // ビデオ ストリームの場合、この情報は必要に応じてパレット情報を含む BITMAPINFO 構造体である。オーディオ ストリームの場合、この情報は WAVEFORMATEX 構造体である。

            // Videoｽﾄﾘｰﾑ用の'strl'チャンク
            {
                var strl_list = hdrlList.CreateList("strl");
                {
                    var chunk = strl_list.CreateChunk("strh");
                    var strh = new AVISTREAMHEADER();
                    strh.fccType = ToFourCC("vids");
                    strh.fccHandler = ToFourCC(fourCC);
                    strh.dwScale = 1000 * 1000; // fps = dwRate / dwScale。秒間30フレームであることをあらわすのにdwScale=33333、dwRate=1000000という場合もあればdwScale=1、dwRate=30という場合もあります. For video streams, this is the frame rate. 
                    strh.dwRate = (int)(videoFormat.FramesPerSec * strh.dwScale);
                    strh.dwLength = videoFrames;
                    strh.dwSuggestedBufferSize = 0x100000;
                    strh.dwQuality = -1; // Quality is represented as a number between 0 and 10,000. If set to –1, drivers use the default quality value.

                    var data = StructureToBytes(strh);
                    chunk.Write(data);
                    chunk.Close();
                }
                {
                    var chunk = strl_list.CreateChunk("strf");
                    var strf = new BITMAPINFOHEADER();
                    strf.biWidth = videoFormat.Width;
                    strf.biHeight = videoFormat.Height;
                    strf.biBitCount = 24;
                    strf.biSizeImage = strf.biHeight * ((3 * strf.biWidth + 3) / 4) * 4; // らしい
                    strf.biCompression = ToFourCC(fourCC);
                    strf.biSize = System.Runtime.InteropServices.Marshal.SizeOf(strf);
                    strf.biPlanes = 1;

                    var data = StructureToBytes(strf);
                    chunk.Write(data);
                    chunk.Close();
                }
                strl_list.Close();
            }
            
            // Audioｽﾄﾘｰﾑ用の'strl'チャンク(あれば)
            if (audioFormat != null)
            {
                var strl_list = hdrlList.CreateList("strl");
                {
                    var chunk = strl_list.CreateChunk("strh");
                    var strh = new AVISTREAMHEADER();
                    strh.fccType = ToFourCC("auds");
                    strh.fccHandler = 0x00; // For audio and video streams, this specifies the codec for decoding the stream. pcmは不要。
                    strh.dwScale = audioFormat.Channels;// For audio streams, this rate corresponds to the time needed to play nBlockAlign bytes of audio, which for PCM audio is the just the sample rate.
                    strh.dwRate = audioFormat.SamplesPerSec * strh.dwScale; // Dividing dwRate by dwScale gives the number of samples per second.
                    strh.dwSampleSize = (short)(audioFormat.Channels * (audioFormat.BitsPerSample / 8)); // For audio streams, this number should be the same as the nBlockAlign member of the WAVEFORMATEX structure describing the audio.
                    strh.dwLength = audioFrames;
                    strh.dwSuggestedBufferSize = strh.dwRate; // ?
                    strh.dwQuality = -1; // Quality is represented as a number between 0 and 10,000. If set to –1, drivers use the default quality value.

                    var data = StructureToBytes(strh);
                    chunk.Write(data);
                    chunk.Close();
                }
                {
                    const int WAVE_FORMAT_PCM = 0x0001;
                    var chunk = strl_list.CreateChunk("strf");
                    var strf = new WAVEFORMATEX();
                    strf.nChannels = audioFormat.Channels;
                    strf.wFormatTag = WAVE_FORMAT_PCM;
                    strf.nSamplesPerSec = audioFormat.SamplesPerSec;
                    strf.nBlockAlign = (short)(audioFormat.Channels * (audioFormat.BitsPerSample / 8));
                    strf.nAvgBytesPerSec = strf.nSamplesPerSec * strf.nBlockAlign;
                    strf.wBitsPerSample = audioFormat.BitsPerSample;
                    strf.cbSize = 0;

                    var data = StructureToBytes(strf);
                    chunk.Write(data);
                    chunk.Close();
                }
                strl_list.Close();
            }
        }

        private class Idx1Entry
        {
            public string ChunkId { get; private set; }
            public int Length { get; private set; }
            public bool Padding { get; private set; }

            public Idx1Entry(string chunkId, int length, bool padding)
            {
                this.ChunkId = chunkId;
                this.Length = length;
                this.Padding = padding;
            }
        }

        // たとえば、ストリーム 0 にオーディオが含まれる場合、そのストリームのデータ チャンクは FOURCC '00wb' を持つ。
        // ストリーム 1 にビデオが含まれる場合、そのストリームのデータ チャンクは FOURCC '01db' または '01dc' を持つ。
        private static Idx1Entry WriteMoviList(RiffList moviList, string chunkId, byte[] data)
        {
            var chunk = moviList.CreateChunk(chunkId);
            chunk.Write(data);

            // データはワード境界に配置しなければならない
            // バイト数が奇数の場合は、1バイトのダミーデータを書き込んでワード境界にあわせる
            int length = data.Length;
            bool padding = false;
            if (length % 2 != 0)
            {
                chunk.WriteByte(0x00); // 1バイトのダミーを書いてワード境界にあわせる
                padding = true;
            }

            chunk.Close();

            return new Idx1Entry(chunkId, length, padding);
        }

        // インデックスには、データ チャンクのリストとファイル内でのその位置が含まれている。
        // インデックスは、AVIOLDINDEX 構造体で構成され、各データ チャンクのエントリが含まれている。
        // ファイルにインデックスが含まれる場合、AVIMAINHEADER 構造体の dwFlags メンバにある AVIF_HASINDEX フラグを設定する。
        private static void WriteIdx1Chunk(RiffFile riff, List<Idx1Entry> IndexList)
        {
            const int AVIIF_KEYFRAME = 0x00000010; // 前後のフレームの情報なしにこのフレームの完全な情報を含んでいる
            var chunk = riff.CreateChunk("idx1");

            int offset = 4;
            foreach (var item in IndexList)
            {
                int length = item.Length;

                chunk.Write(ToFourCC(item.ChunkId));
                chunk.Write(AVIIF_KEYFRAME);
                chunk.Write(offset);
                chunk.Write(length);

                offset += 8 + length; // 8は多分00dcとﾃﾞｰﾀｻｲｽﾞ
                if (item.Padding) offset += 1;
            }

            chunk.Close();
        }

        private static int ToFourCC(string fourCC)
        {
            if (fourCC.Length != 4) throw new ArgumentException("must be 4 characters long.", "fourCC");
            return ((int)fourCC[3]) << 24 | ((int)fourCC[2]) << 16 | ((int)fourCC[1]) << 8 | ((int)fourCC[0]);
        }

        #region "Struncture Marshalling"

        private static byte[] StructureToBytes<T>(T st) where T : struct
        {
            int size = System.Runtime.InteropServices.Marshal.SizeOf(st);
            IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
            System.Runtime.InteropServices.Marshal.StructureToPtr(st, ptr, false);

            byte[] data = new byte[size];
            System.Runtime.InteropServices.Marshal.Copy(ptr, data, 0, size);

            System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
            return data;
        }
        
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct AVIMAINHEADER
        {
            public UInt32 dwMicroSecPerFrame;  // only used with AVICOMRPESSF_KEYFRAMES
            public UInt32 dwMaxBytesPerSec;
            public UInt32 dwPaddingGranularity; // only used with AVICOMPRESSF_DATARATE
            public UInt32 dwFlags;
            public UInt32 dwTotalFrames;
            public UInt32 dwInitialFrames;
            public UInt32 dwStreams;
            public UInt32 dwSuggestedBufferSize;
            public UInt32 dwWidth;
            public UInt32 dwHeight;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public UInt32[] dwReserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct RECT
        {
            public Int16 left;
            public Int16 top;
            public Int16 right;
            public Int16 bottom;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct AVISTREAMHEADER
        {
            public Int32 fccType;
            public Int32 fccHandler;
            public Int32 dwFlags;
            public Int16 wPriority;
            public Int16 wLanguage;
            public Int32 dwInitialFrames;
            public Int32 dwScale;
            public Int32 dwRate;
            public Int32 dwStart;
            public Int32 dwLength;
            public Int32 dwSuggestedBufferSize;
            public Int32 dwQuality;
            public Int32 dwSampleSize;
            public RECT rcFrame;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BITMAPINFOHEADER
        {
            public Int32 biSize;
            public Int32 biWidth;
            public Int32 biHeight;
            public Int16 biPlanes;
            public Int16 biBitCount;
            public Int32 biCompression;
            public Int32 biSizeImage;
            public Int32 biXPelsPerMeter;
            public Int32 biYPelsPerMeter;
            public Int32 biClrUsed;
            public Int32 biClrImportant;
        }


        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct WAVEFORMATEX
        {
            public Int16 wFormatTag;
            public Int16 nChannels;
            public Int32 nSamplesPerSec;
            public Int32 nAvgBytesPerSec;
            public Int16 nBlockAlign;
            public Int16 wBitsPerSample;
            public Int16 cbSize;
        }

        #endregion
    }
}
