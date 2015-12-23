using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Windows.Forms;
using SdlDotNet.Core;
using SdlDotNet.Graphics;
using SdlDotNet.Graphics.Primitives;
using SdlDotNet.Input;
using Font = SdlDotNet.Graphics.Font;

namespace PatternRecognition1
{
	static class Program
	{
		// 特徴ベクトルの大きさ
		// データファイルもこのサイズにしないとダメ
		const int Size = 5 * 5;
		const int Width = 5;

		// パターン1ドットの大きさ
		const int Box = 70;
		const int Margin = 20;

		// 学習係数
		const double P = 0.3;

		// スリープ
		const int Sleep = 0;

		/// <summary>
		/// ファイルからデータ作成
		/// </summary>
		/// <param name="fileName">ファイル名</param>
		/// <returns>作成したデータ</returns>
		static private List<Tuple<char, double, List<double>>> Initialize(string fileName)
		{
			var data = new List<Tuple<char, double, List<double>>>();
			// データファイル読み込み
			// 最初の行はクラス(1文字)
			// 次の5×5で特徴ベクトル
			using (var sr = new StreamReader(fileName))
			{
				var line = sr.ReadLine();
				while (line != null)
				{
					if (line.Length <= 0)
						continue;
					var lineData = new List<double>();
					var c = line[0];
					for (var i = 0; i < Width; ++i)
					{
						line = sr.ReadLine();
						if (line == null)
							break;
						lineData.AddRange(line.Select(x => (x == ' ' ? 0.0 : 1.0)).ToList());
					}
					data.Add(Tuple.Create(c, 0.0, lineData));
					line = sr.ReadLine();
				}
			}
			return data;
		}
		/// <summary>
		/// ユーザデータの初期化
		/// </summary>
		/// <returns>ユーザデータ</returns>
		static private List<double> InitUserData()
		{
			var data = new List<double>();
			for (var i = 0; i < Size; ++i)
				data.Add(0);
			return data;
		}

		/// <summary>
		/// まいんちゃん
		/// </summary>
		/// <param name="args">データファイル名</param>
		[STAThread]
		static private void Main(string[] args)
		{
			// 引数でデータファイル名を指定する
			if (args.Length != 1)
			{
				Console.WriteLine("PatternRecognition1 DATAFILE");
				return;
			}
			var data = Initialize(args[0]);
			var userData = InitUserData();
			// 画面作成
			Video.WindowCaption = "HELLO SDL.NET WORLD";
			Surface screen = Video.SetVideoMode(Margin * 2 + Box * Width, Margin * 2 + Box * Size / Width + 100);
			// イベントハンドラ設定
			Events.Quit += (object s, QuitEventArgs e) => { Events.QuitApplication(); };
			Events.MouseButtonDown += (object s, MouseButtonEventArgs e) => {
				// クリックした部分を反転
				var x = (e.X - Margin < 0 ? -1 : (e.X - Margin) / Box);
				var y = (e.Y - Margin < 0 ? -1 : (e.Y - Margin) / Box);
				if (0 <= x && x < Width && 0 <= y && y < Size / Width)
				{
					// x, y = クリックしたマス
					userData[y * Size / Width + x] = (userData[y * Size / Width + x] == 0 ? 1 : 0);
					data = RecognizeLogic(data, userData);
				}
				// クリックしたクラスを学習
				x = (e.X - Margin < 0 ? -1 : (e.X - Margin) / (Box / 2));
				y = e.Y;
				if (0 <= x && x < data.Count && 
					Margin * 2 + Box * Size / Width <= y && y < Margin * 2 + Box * Size / Width + Box / 2)
				{
					// x = クリックしたプロトタイプ(dataのx番目(0～))
					screen.Fill(Color.Red);
					screen.Update();
					Thread.Sleep(100);

					data = TraningLogic(x, data, userData);
					data = RecognizeLogic(data, userData);
				}
			};
			Events.Tick += (object s, TickEventArgs e) =>
			{
				// パターン認識本体
				screen.Fill(Color.White);
				Draw(screen, data, userData);
				Video.Update();
				if (Sleep > 0)
					Thread.Sleep(Sleep);
			};
			Events.Run();
		}

		/// <summary>
		/// 識別関数(g)メインロジック
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		static private List<Tuple<char, double, List<double>>> RecognizeLogic(List<Tuple<char, double, List<double>>> data, List<double> userData)
		{
			for (var i = 0; i < data.Count; ++i)
			{
				var pp = data[i];
				var p = pp.Item3;
				var d = -0.5f * p.Aggregate((acc, succ) => acc + Math.Abs(succ) * Math.Abs(succ));	// w0 * x0(≡1), w0 = -1/2 * ||pi||^2
				for (var j = 0; j < p.Count; ++j)	// Σ(w1～wd) * (x1～xd)
					d += p[j] * userData[j];
				data[i] = Tuple.Create(pp.Item1, (double)d, pp.Item3);
			}
            Console.WriteLine("----------");
            foreach (var p in data)
                Console.WriteLine("{0} = {1}", p.Item1, p.Item2);
			return data.OrderByDescending((x) => x.Item2).ToList();
		}

		/// <summary>
		/// パーセプトロン学習ロジック
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		static private List<Tuple<char, double, List<double>>> TraningLogic(int sel, List<Tuple<char, double, List<double>>> data, List<double> userData)
		{
			var pj = data[0].Item3;		// 今はjだけど
			var pi = data[sel].Item3;	// iが正しい
			// 学習する
			for (int i = 0; i < userData.Count; ++i)
			{
				pi[i] = pi[i] + P * userData[i];
				pj[i] = pj[i] - P * userData[i];
			}

			return data;
		}

		/// <summary>
		/// 描画
		/// </summary>
		/// <param name="screen">SDLサーフェイス</param>
		/// <param name="data">描画データ(Width×Height)</param>
		static private void Draw(Surface screen, List<Tuple<char, double, List<double>>> data, List<double> userData)
		{
			// ユーザの描画
			for (var y = 0; y < Size / Width; ++y)
			{
				for (var x = 0; x < Width; ++x)
				{
					if (userData[y * Width + x] != 0)
					{
						var rect = new Rectangle(Margin + x * Box, Margin + y * Box, Box, Box);
						screen.Fill(rect, Color.Red);
					}
				}
			}
			// 罫線
			for (var y = 0; y <= Size / Width; ++y)
			{
				var line = new Line(new Point(Margin, Margin + y * Box), new Point(Margin + Width * Box, Margin + y * Box));
				screen.Draw(line, Color.Black);
			}
			for (var x = 0; x <= Width; ++x)
			{
				var line = new Line(new Point(Margin + x * Box, Margin), new Point(Margin + x * Box, Margin + Width * Box));
				screen.Draw(line, Color.Black);
			}
			// クラス一覧
			using (var font = new Font(@"GenShinGothic-Normal.ttf", 24))
			{
				for (var i = 0; i < data.Count; ++i)
				{
					var c = data[i].Item1;
					using (var surface = font.Render(c.ToString(), Color.PaleVioletRed))
					{
						screen.Blit(surface, new Point(Margin + i * Box / 2 + Box / 5, Margin * 2 + Box * Size / Width));
					}
				}
			}
			// 罫線
			for (var y = 0; y <= 1; ++y)
			{
				var line = new Line(new Point(Margin, Margin * 2 + Box * Size / Width + y * Box / 2), new Point(Margin + Width * Box, Margin * 2 + Box * Size / Width + y * Box / 2));
				screen.Draw(line, Color.Black);
			}
			for (var x = 0; x <= Width * 2; ++x)
			{
				var line = new Line(new Point(Margin + x * Box / 2, Margin * 2 + Box * Size / Width), new Point(Margin + x * Box / 2, Margin * 2 + Box * Size / Width + Box / 2));
				screen.Draw(line, Color.Black);
			}
			using (var font = new Font(@"GenShinGothic-Normal.ttf", 18))
			{
				using (var surface = font.Render("一番左が認識された数字です。", Color.Peru))
				{
					screen.Blit(surface, new Point(Margin, Margin * 2 + Box * Size / Width + Box / 2));
				}
				using (var surface = font.Render("違ったら数字をクリックで(少し)学習！！", Color.Blue))
				{
					screen.Blit(surface, new Point(Margin, Margin * 2 + Box * Size / Width + Box));
				}
			}
		}
	}
}
