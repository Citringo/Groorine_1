﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Groorine.Api;
using Groorine.Synth;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using GPlayer = Groorine.Player;
using P = System.IO.Path;
using F = System.IO.File;
using D = System.IO.Directory;
using Groorine.DataModel;

namespace Groorine.DotNet45
{
	public class FileSystem : IFileSystem
	{
		public IFolder BaseFolder => new Folder(AppDomain.CurrentDomain.BaseDirectory);

		public IFolder LocalFolder => new Folder(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
	}

	public class Folder : IFolder
	{
		public Folder(string path)
		{
			Name = P.GetFileName(path);
			Path = path;
		}

		public string Name { get; }
		public string Path { get; }

		public async Task<IFile> GetFileAsync(string name) => await Task.Factory.StartNew(() => new File(P.Combine(Path, name)));
		public async Task<IList<IFile>> GetFilesAsync() => await Task.Factory.StartNew(() => D.EnumerateFiles(Path).Select(s => (IFile)new File(s)).ToList());
		public async Task<IFolder> GetFolderAsync(string name) => await Task.Factory.StartNew(() => new Folder(P.Combine(Path, name)));
		public async Task<IList<IFolder>> GetFoldersAsync() => await Task.Factory.StartNew(() => D.GetDirectories(Path).Select(s => (IFolder)new Folder(s)).ToList());
	}

	public class File : IFile
	{
		public string Name { get; }
		public string Path { get; }

		public async Task<Stream> OpenAsync(FileAccessMode fileAccess)
		{
			switch (fileAccess)
			{
				case FileAccessMode.Read:
					return await Task.Factory.StartNew(() => F.OpenRead(Path));
				case FileAccessMode.ReadAndWrite:
					return await Task.Factory.StartNew(() => F.Open(Path, FileMode.Open, FileAccess.ReadWrite));
				default:
					throw new ArgumentException($"{nameof(fileAccess)} の値が不正です。");
			}
		}

		public File(string path)
		{
			Name = P.GetFileName(path);
			Path = path;
		}

	}

	public class Player : IDisposable
    {
		/// <summary>
		/// 現在の Groorine プレイヤーを取得します。
		/// </summary>
		public GPlayer CorePlayer { get; private set; }

		/// <summary>
		/// プレイヤークラスが音声ファイルを書き込むために使用するバッファーです。
		/// </summary>
		private short[] _buffer;

		private BufferedWaveProvider _bwp;

		private IWavePlayer _nativeplayer;

		private CancellationTokenSource _cts;
		
		public Player(int sampleRate = 44100, int latency = 50)
		{
			var task = new Task(async () => await InitializeAsync(sampleRate, latency));
			task.RunSynchronously();
			task.Dispose();
		}

		public bool IsPlaying { get; private set; }

		private async Task InitializeAsync(int sampleRate, int latency)
		{
			_bwp = new BufferedWaveProvider(new WaveFormat(44100, 16, 2));
			//var mde = new MMDeviceEnumerator();
			//var mmDevice = mde.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
			await AudioSourceManager.InitializeAsync(new FileSystem()).ConfigureAwait(false);
			CorePlayer = new GPlayer(sampleRate);
			_buffer = CorePlayer.CreateBuffer(latency);
			//_buffer = new short[882];
			_cts = new CancellationTokenSource();
			_nativeplayer = new WasapiOut(AudioClientShareMode.Shared, true, 100);
			_nativeplayer.Init(_bwp);
			_nativeplayer.Play();
		}

	    /// <summary>
	    /// MIDI ファイルのパスを指定して、再生を開始します。
	    /// </summary>
	    /// <param name="filePath">ファイルパス。</param>
	    /// <param name="loopCount"></param>
	    /// <param name="fadeOutTime"></param>
	    public void Play(string filePath, int loopCount = -1, int fadeOutTime = 2000)
		{
			// 正しく生成されていればこれらはnullでないはずなので、例外が投げられたらバグが起きているはず
			if (CorePlayer == null || 
				_nativeplayer == null ||
				_cts == null ||
				_buffer == null
				)
				throw new InvalidOperationException("初期化が完了していません。");

			CorePlayer.Load(SmfParser.Parse(F.OpenRead(P.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath))));
			
			PlayAsync(loopCount, fadeOutTime);
		}
		

	    public async void PlayAsync(int loopCount = -1, int fadeOutTime = 2000)
	    {
			await Task.Run(async () =>
			{
				CorePlayer.Play(loopCount, fadeOutTime);

				IsPlaying = true;
				while (true)
				{
					CorePlayer.GetBuffer(_buffer);

					if (!CorePlayer.IsPlaying || _cts.IsCancellationRequested)
						break;

					var b = ToByte(_buffer);


					_bwp.AddSamples(b, 0, b.Length);

					while (_bwp.BufferedBytes > _buffer.Length * 8)
						await Task.Delay(1);

				}
				IsPlaying = false;
			});
		}

		public async Task SaveAsync(string path, int loopCount = 2, MidiFile mf = null)
		{
			CorePlayer.Stop();
			if (mf != null)
				CorePlayer.Load(mf);
			CorePlayer.Play(loopCount);

			using (var wfw = new WaveFileWriter(path, new WaveFormat()))
			{
				while (CorePlayer.IsPlaying)
				{
					byte[] b = ToByte(CorePlayer.GetBuffer(_buffer));
					await wfw.WriteAsync(b, 0, b.Length);
				}
			}
					
		}
		

		public async Task StopAsync()
		{
			await Task.Run(async () =>
			{
				CorePlayer.Stop();
				while (IsPlaying)
					await Task.Delay(1);
			});
		}

		private static byte[] ToByte(short[] a)
		{
			var size = a.Length * sizeof(short);
			var b = new byte[size];
			unsafe
			{
				
				fixed (short* psrc = &a[0])
				{
					using (var strmSrc = new UnmanagedMemoryStream((byte*)psrc, size))
						strmSrc.Read(b, 0, size);
				}
			}
			return b;
		}

		#region IDisposable Support
		private bool _disposedValue; // 重複する呼び出しを検出するには

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					// TODO: マネージ状態を破棄します (マネージ オブジェクト)。
					_nativeplayer?.Dispose();
				}

				// TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
				// TODO: 大きなフィールドを null に設定します。

				_disposedValue = true;
			}
		}

		// TODO: 上の Dispose(bool disposing) にアンマネージ リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
		// ~Player() {
		//   // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
		//   Dispose(false);
		// }

		// このコードは、破棄可能なパターンを正しく実装できるように追加されました。
		public void Dispose()
		{
			// このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
			Dispose(true);
			// TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
			// GC.SuppressFinalize(this);
		}
		#endregion
	}
}
