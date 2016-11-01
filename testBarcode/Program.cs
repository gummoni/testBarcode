/*
   MIT License

   Copyright (c) 2016 Kouichi Nishino

   Permission is hereby granted, free of charge, to any person obtaining a copy
   of this software and associated documentation files (the "Software"), to deal
   in the Software without restriction, including without limitation the rights
   to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
   copies of the Software, and to permit persons to whom the Software is
   furnished to do so, subject to the following conditions:

   The above copyright notice and this permission notice shall be included in all
   copies or substantial portions of the Software.

   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
   IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
   FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
   AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
   LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
   OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
   SOFTWARE.
*/
using System;
using System.Collections.Generic;
using System.Drawing;

namespace testBarcode
{

    class Program
    {
        static void Main(string[] args)
        {
            //参考URL
            // https://www.technical.jp/barcode/handbook1/chapter-6-1.html

            //解析手順
            //１．バーパターンを{0＝解析不能、1=短いバー、2=長いバー、白黒交互になっている}の組み合わせにする
            //２．パターンテーブルを用いてバーコード情報を文字列化する


            //カメラ画像を取り込んでバーコードパターンを取得する
            //１．ライン情報を抜き取り、２値化する。  11119911119999 -> 00001100001111
            //    Y(輝度) = 0.299 x R + 0.587 x G + 0.114 x B　又は Y = (R+G+B)/3
            //２．数値配列に変換する。　00001100001111 -> 4244
            //３．最小に近ければ１、最大に近ければ２にする   4244 -> 2122


            //テストバーコードパターンで正しく認識できるかテスト
            var itf = new ITF();
            foreach (var ret in itf.Parse("0000011111211212112122211112112112211211121211212211211221121100000111112112121121222111121121122112111212112122110000"))
            {
                Console.WriteLine(ret);
            }

            //画像からラインバーコードパターンデータを取り出して解析してバーコードが正しく認識できるかテスト
            foreach (var line in new CCD().Scan())
            {
                foreach (var ret in itf.Parse(line))
                {
                    Console.WriteLine(ret);
                }
            }


            //code39
            //*A12345P*
            //011010011001010010101110100100110110101001011001101010011001001101010110101100101001100101110010101010011001011010111010010010

            //*$20 +3B5*
            //01101001001011011011011101010100111010100101011100100101011100101001011011010110110110010011101010101001011101001001011100101010111010010010

            //nw7

            Console.ReadKey();
        }
    }


    public class CCD
    {
        int threshold = 64;     //白黒判定の閾値
        int paddingSize = 20;   //空白判定の長さ

        /// <summary>
        /// スキャン
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> Scan()
        {
            //テストデータの用意
            var bmp = new Bitmap(@"itf.bmp");
            var data = new byte[bmp.Width, bmp.Height];
            for (var y = 0; y < bmp.Height; y++)
            {
                for (var x = 0; x < bmp.Width; x++)
                {
                    var col = bmp.GetPixel(x, y);
                    var val = (byte)((col.R + col.G + col.B) / 3);
                    data[x, y] = val;
                }
            }

            //スキャン結果を返す
            return ReadLine(data, bmp.Width, bmp.Height);
        }


        //1ラインずつライン情報を返す(白が10ピクセルあったらそこからSTXコードがあるか検索、次の空白エリアまでデータを取る)
        IEnumerable<string> ReadLine(byte[,] rgb_data, int width, int height)
        {

            for (var y = 0; y < height; y++)
            {
                var px = 0;
                var result = new List<int>();
                var min = width;                            //最小値
                var max = 0;                                //最大値
                var bar_val = threshold <= rgb_data[0, y];  //現在の色
                var bar_count = 1;                          //カウント数
                var state = 0;                              //未検知でスタート

                for (var x = 0; x < width; x++)
                {
                    //データ取り込み
                    var val = threshold <= rgb_data[x, y];

                    if (val == bar_val)
                    {
                        //変化無し
                        bar_count++;
                        if ((paddingSize <= bar_count) && bar_val && (state == 1))
                        {
                            //取込中に白ベタ領域検知
                            var barcode = "";
                            foreach (var value in result)
                            {
                                // 2値化｛1=短い、2=長い｝
                                var lo = Math.Abs(value - min);
                                var hi = Math.Abs(value - max);
                                barcode += (lo < hi) ? 1 : 2;       //最小値に近い方を１、最大値に近い方を２にする
                            }

                            string ST_CODE = "1111";
                            string ED_CODE = "211";
                            var len = barcode.Length;
                            var endpos = len - ED_CODE.Length;
                            if ((barcode.IndexOf(ST_CODE) == 0) && (barcode.LastIndexOf(ED_CODE) == endpos))
                            {
                                if (0 == ((len - ST_CODE.Length - ED_CODE.Length) % 10))
                                {
                                    //結果を返す
                                    Console.WriteLine($"x={px}, y={y}: {barcode}");
                                    yield return barcode + "0";
                                }
                            }

                            //取込完了
                            state = 0;
                        }
                    }
                    else
                    {
                        //変化あり
                        if (paddingSize > bar_count)
                        {
                            //バーコード？
                            if (1 == state)
                            {
                                //取込中
                                if (min > bar_count) min = bar_count;
                                if (max < bar_count) max = bar_count;
                                result.Add(bar_count);
                            }
                        }
                        else if (!bar_val)
                        {
                            //黒のベタ領域は検知エラー
                            state = 0;
                        }
                        else if (state == 0)
                        {
                            //取込開始
                            state = 1;
                            min = width;
                            max = 0;
                            px = x;
                            result.Clear();
                        }
                        //更新
                        bar_count = 1;
                        bar_val = val;
                    }
                }
            }
        }
    }

    public interface IBarcodeFormat
    {
        IEnumerable<string> Parse(string pattern);
    }

    public class ITF : IBarcodeFormat
    {
        //ITF2桁組み合わせパターン定義
        //解析{0＝解析不能、1=短いバー、2=長いバー、白黒交互になっている}
        static readonly Dictionary<string, int> DD = new Dictionary<string, int>()
        {
            { "1111222211", 00 },
            { "1211212112", 01 },
            { "1112212112", 02 },
            { "1212212111", 03 },
            { "1111222112", 04 },
            { "1211222111", 05 },
            { "1112222111", 06 },
            { "1111212212", 07 },
            { "1211212211", 08 },
            { "1112212211", 09 },
            { "2111121221", 10 },
            { "2211111122", 11 },
            { "2112111122", 12 },
            { "2212111121", 13 },
            { "2111121122", 14 },
            { "2211121121", 15 },
            { "2112121121", 16 },
            { "2111111222", 17 },
            { "2211111221", 18 },
            { "2112111221", 19 },
            { "1121121221", 20 },
            { "1221111122", 21 },
            { "1122111122", 22 },
            { "1222111121", 23 },
            { "1121121122", 24 },
            { "1221121121", 25 },
            { "1122121121", 26 },
            { "1121111222", 27 },
            { "1221111221", 28 },
            { "1122111221", 29 },
            { "2121121211", 30 },
            { "2221111112", 31 },
            { "2122111112", 32 },
            { "2222111111", 33 },
            { "2121121112", 34 },
            { "2221121111", 35 },
            { "2122121111", 36 },
            { "2121111212", 37 },
            { "2221111211", 38 },
            { "2122111211", 39 },
            { "1111221221", 40 },
            { "1211211122", 41 },
            { "1112211122", 42 },
            { "1212211121", 43 },
            { "1111221122", 44 },
            { "1211221121", 45 },
            { "1112221121", 46 },
            { "1111211222", 47 },
            { "1211211221", 48 },
            { "1112211221", 49 },
            { "2111221211", 50 },
            { "2211211112", 51 },
            { "2112211112", 52 },
            { "2212211111", 53 },
            { "2111221112", 54 },
            { "2211221111", 55 },
            { "2112221111", 56 },
            { "2111211212", 57 },
            { "2211211211", 58 },
            { "2112211211", 59 },
            { "1121221211", 60 },
            { "1221211112", 61 },
            { "1122211112", 62 },
            { "1222211111", 63 },
            { "1121221112", 64 },
            { "1221221111", 65 },
            { "1122221111", 66 },
            { "1121211212", 67 },
            { "1221211211", 68 },
            { "1122211211", 69 },
            { "1111122221", 70 },
            { "1211112122", 71 },
            { "1112112122", 72 },
            { "1212112121", 73 },
            { "1111122122", 74 },
            { "1211122121", 75 },
            { "1112122121", 76 },
            { "1111112222", 77 },
            { "1211112221", 78 },
            { "1112112221", 79 },
            { "2111122211", 80 },
            { "2211112112", 81 },
            { "2112112112", 82 },
            { "2212112111", 83 },
            { "2111122112", 84 },
            { "2211122111", 85 },
            { "2112122111", 86 },
            { "2111112212", 87 },
            { "2211112211", 88 },
            { "2112112211", 89 },
            { "1121122211", 90 },
            { "1221112112", 91 },
            { "1122112112", 92 },
            { "1222112111", 93 },
            { "1121122112", 94 },
            { "1221122111", 95 },
            { "1122122111", 96 },
            { "1121112212", 97 },
            { "1221112211", 98 },
            { "1122112211", 99 },
        };
        static readonly string ST = "1111";
        static readonly string ED = "2110";

        public IEnumerable<string> Parse(string pattern)
        {
            var dat = "";
            var state = 0;
            var result = "";
            foreach (var ch in pattern)
            {
                dat += ch;
                switch (state)
                {
                    case 0: //始端検索中
                        if (dat.Contains(ST))
                        {
                            //始端検知
                            dat = "";
                            state = 1;
                            result = "";
                        }
                        break;
                    case 1: //パターン解析中
                        if (dat == ED)
                        {
                            //終端検知
                            dat = "";
                            state = 0;
                            yield return result;
                        }
                        else if (DD.ContainsKey(dat))
                        {
                            //パターン検知
                            result += string.Format("{0:D2}", DD[dat]);
                            dat = "";
                        }
                        break;
                }
            }
        }
    }

    public class CODE39 : IBarcodeFormat
    {
        //１キャラクタは細バー６本、太バー３本の計９本で構成されています。
        //細バーと太バーの比率は１：２５以上です。
        //スペースコードの後ろには、余白部として１キャラクタ分以上のスペースが必要です。
        //キャラクタとキャラクタの間には、細バーの幅以上のキャラクタギャップが必要です。
        public CODE39()
        {
            // 1:100100001
            // 2:001100001
            // 3:101100000
            // 4:000110001
            // 5:100110000
            // 6:001110000
            // 7:000100101
            // 8:100100100
            // 9:001100100
            // 0:000110100
            // A:100001001
            // B:001001001
            // C:101001000
            // D:000011001
            // E:100011000
            // F:001011000
            // G:000001101
            // H:100001100
            // I:001001100
            // J:000011100
            // K:100000011
            // L:001000011
            // M:101000010
            // N:000010011
            // O:100010010
            // P:001010010
            // Q:000000111
            // R:100000110
            // S:001000110
            // T:000010110
            // U:110000001
            // V:011000001
            // W:111000000
            // X:010010001
            // Y:110010000
            // Z:011010000
            // _:010000101
            // .:110000100
            //  :011000100
            // *:010010100
            // $:010101000
            // /:010100010
            // +:010001010
            // %:000101010
        }

        public IEnumerable<string> Parse(string pattern)
        {
            throw new NotImplementedException();
        }
    }

    public class NW7 : IBarcodeFormat
    {
        //細バーと太バー合わせて７本で構成されています。
        //数字と一部特殊記号は細バー５本、太バー２本で構成されており、
        //その他は細バー４本、太バー３本で構成されています。
        //細バーと太ばーとの比率は１：２５以上です。
        //スタートとストップコード通常Ａ，Ｂ，Ｃ，Ｄのアルファベットコードを使用しています。
        //スタートコードの前とストップコードの後ろには、余白部として１キャラクタ分以上のスーペースが必要です。
        //キャラクタとキャラクタとの間には、細バーの幅以上のキャラクタギャップが必要です。
        public NW7()
        {
            // A:0011010
            // B:0101001
            // C:0001011
            // D:0001110
            // .:1010100
            // +:0010101
            // ::1000101
            // /:1010001
            // $:0011000
            // _:0001100
            // 0:0000011
            // 1:0000110
            // 2:0001001
            // 3:1100000
            // 4:0010010
            // 5:1000010
            // 6:0100001
            // 7:0100100
            // 8:0110000
            // 9:1001000
        }

        public IEnumerable<string> Parse(string pattern)
        {
            throw new NotImplementedException();
        }
    }

}
