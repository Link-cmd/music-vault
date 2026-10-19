using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace MusicVault {
class Song {
 public string Name; public string Extension; public long Offset, Length; public string Hash;
 public string Number {get;set;}
 public string DisplayTitle {get {return Name;}}
 public string Format {get {return Extension.TrimStart('.').ToUpperInvariant();}}
 public override string ToString() { return Name; }
}
static partial class Vault {
 public static readonly byte[] Magic = Encoding.ASCII.GetBytes("MVLT0001");
 public static string Self { get { return System.Reflection.Assembly.GetExecutingAssembly().Location; } }
 public static List<Song> Read(string path) {
  using (var s = File.OpenRead(path)) using (var r = new BinaryReader(s, Encoding.UTF8)) {
   if(s.Length < 16) return new List<Song>();
   s.Position=s.Length-16; long index=r.ReadInt64();
   if(!r.ReadBytes(8).SequenceEqual(Magic)) return new List<Song>();
   if(index<0 || index>s.Length-20) throw new InvalidDataException("Invalid music index.");
   s.Position=index; int count=r.ReadInt32();
   if(count<0 || count>1000000) throw new InvalidDataException("Invalid song count.");
   var songs=new List<Song>();
   for(int i=0;i<count;i++) {
    var song=new Song {Name=r.ReadString(),Extension=r.ReadString(),Offset=r.ReadInt64(),Length=r.ReadInt64(),Hash=r.ReadString()};
    if(song.Offset<0 || song.Length<0 || song.Offset>index-song.Length) throw new InvalidDataException("Invalid song data.");
    songs.Add(song);
   }
   return songs;
  }
 }
 public static string CopySong(string source, Song song, Stream output) {
  using(var input=File.OpenRead(source)) using(var hash=SHA256.Create()) {
   input.Position=song.Offset; long remaining=song.Length; byte[] b=new byte[1024*1024];
   while(remaining>0) { int n=input.Read(b,0,(int)Math.Min(b.Length,remaining)); if(n==0) throw new EndOfStreamException(); output.Write(b,0,n); hash.TransformBlock(b,0,n,null,0); remaining-=n; }
   hash.TransformFinalBlock(new byte[0],0,0); return BitConverter.ToString(hash.Hash).Replace("-","");
  }
 }
 public static int Build(string original,string output,string[] files) {
  var songs=Read(original); var hashes=new HashSet<string>(songs.Select(x=>x.Hash)); int added=0;
  using(var dest=new FileStream(output,FileMode.CreateNew,FileAccess.Write)) {
   using(var input=File.OpenRead(original)) input.CopyTo(dest);
   foreach(string path in files) {
    var info=new FileInfo(path); if(!info.Exists) throw new FileNotFoundException("Song not found",path);
    if(!Supported(path)) continue;
    var song=new Song {Name=Path.GetFileNameWithoutExtension(path),Extension=Path.GetExtension(path).ToLowerInvariant(),Offset=dest.Position,Length=info.Length};
    song.Hash=CopySong(path,new Song {Offset=0,Length=info.Length},dest);
    if(hashes.Add(song.Hash)) { songs.Add(song); added++; } else { dest.SetLength(song.Offset); dest.Position=song.Offset; }
   }
   long index=dest.Position;
   using(var writer=new BinaryWriter(dest,Encoding.UTF8,true)) {
    writer.Write(songs.Count);
    foreach(var song in songs) {writer.Write(song.Name);writer.Write(song.Extension);writer.Write(song.Offset);writer.Write(song.Length);writer.Write(song.Hash);}
    writer.Write(index); writer.Write(Magic); writer.Flush();
   }
   dest.Flush(true);
  }
  if(Read(output).Count!=songs.Count) throw new InvalidDataException("The updated collection could not be verified.");
  return added;
 }
 public static bool Supported(string path) { return new[]{".mp3",".wav",".m4a",".wma",".aac"}.Contains(Path.GetExtension(path).ToLowerInvariant()); }
 public static string Quote(string arg) { return "\""+arg.Replace("\"", "")+"\""; }
 public static void Helper(string target,string manifest,int parent,bool remove=false,bool rename=false) {
  var app=new Application(); var status=new TextBlock {Text="Saving your music into the app…\nKeep this window open until it finishes.",Margin=new Thickness(30),FontSize=18,TextWrapping=TextWrapping.Wrap};
  var window=new Window {Title=remove?"Music Vault · Removing song":"Music Vault · Adding songs",Width=480,Height=180,WindowStartupLocation=WindowStartupLocation.CenterScreen,Content=status,ResizeMode=ResizeMode.NoResize};
  if(remove)status.Text="Removing the song from your collection…\nKeep this window open until it finishes.";
  if(rename){window.Title="Music Vault · Renaming song";status.Text="Saving the new song name…\nKeep this window open until it finishes.";}
  bool complete=false; window.Closing+=(s,e)=>{if(!complete)e.Cancel=true;};
  window.ContentRendered+=async(s,e)=>{
   string next=target+"."+Guid.NewGuid().ToString("N")+".new"; string backup=target+".previous";
   try {
    await Task.Run(()=>{
     try {Process.GetProcessById(parent).WaitForExit();} catch(ArgumentException) {}
     string[] files=File.ReadAllLines(manifest,Encoding.UTF8).Select(x=>Encoding.UTF8.GetString(Convert.FromBase64String(x))).ToArray();
     if(rename){if(files.Length!=2)throw new InvalidDataException("Invalid rename request.");Rebuild(target,next,files[0],files[1]);}else if(remove){if(files.Length!=1)throw new InvalidDataException("Invalid deletion request.");Rebuild(target,next,files[0]);}else Build(target,next,files);
     if(File.Exists(backup)) throw new IOException("A recovery copy already exists: "+backup);
     File.Replace(next,target,backup);
    });
    Process.Start(target);
    try {File.Delete(backup);File.Delete(manifest);} catch {}
   } catch(Exception ex) { MessageBox.Show("Your existing collection has been kept.\n\n"+ex.Message,"Unable to update collection"); try {if(File.Exists(next))File.Delete(next); Process.Start(target);}catch{} }
   complete=true; window.Close();
  };
  app.Run(window);
 }
 [STAThread] public static int Main(string[] args) {
  AppDomain.CurrentDomain.AssemblyResolve+=(sender,eventArgs)=>{
   if(new System.Reflection.AssemblyName(eventArgs.Name).Name!="NAudio")return null;
   using(var input=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("NAudio.dll"))using(var buffer=new MemoryStream()){input.CopyTo(buffer);return System.Reflection.Assembly.Load(buffer.ToArray());}
  };
  try {
   if(args.Length>0 && args[0]=="--level") {AutoLevel.Prepare(args[1],args[2],System.Threading.CancellationToken.None);return 0;}
   if(args.Length>0 && args[0]=="--level-vault") {var song=args.Length>3?Read(args[1]).First(x=>x.Name.IndexOf(args[3],StringComparison.OrdinalIgnoreCase)>=0):Read(args[1])[0];string source=args[2]+song.Extension;try{using(var output=File.Create(source))CopySong(args[1],song,output);AutoLevel.Prepare(source,args[2],System.Threading.CancellationToken.None);}finally{File.Delete(source);}return 0;}
   if(args.Length>0 && args[0]=="--reskin") {Rebuild(args[1],args[2]);return 0;}
   if(args.Length>0 && args[0]=="--remove-copy") {Rebuild(args[1],args[2],args[3]);return 0;}
   if(args.Length>0 && args[0]=="--rename-copy") {Rebuild(args[1],args[2],args[3],args[4]);return 0;}
   if(args.Length>0 && args[0]=="--pack") {Build(Self,args[1],Directory.GetFiles(args[2]).Where(Supported).OrderBy(x=>x,StringComparer.CurrentCultureIgnoreCase).ToArray());return 0;}
   if(args.Length>0 && args[0]=="--verify") {
    var songs=Read(Self); foreach(var song in songs) if(CopySong(Self,song,Stream.Null)!=song.Hash) throw new InvalidDataException(song.Name);
    File.WriteAllText(args[1],"Verified "+songs.Count+" songs; all SHA-256 checksums match."); return 0;
   }
   if(args.Length>0 && args[0]=="--update") {Helper(args[1],args[2],int.Parse(args[3]));return 0;}
   if(args.Length>0 && args[0]=="--delete") {Helper(args[1],args[2],int.Parse(args[3]),true);return 0;}
   if(args.Length>0 && args[0]=="--rename") {Helper(args[1],args[2],int.Parse(args[3]),false,true);return 0;}
   var app=new Application(); var window=new Player();
   if(args.Length>0 && args[0]=="--smoke") window.Loaded+=(s,e)=>window.Smoke(args[1],args.Length>2?args[2]:null);
   if(args.Length>0 && args[0]=="--modes") window.Loaded+=(s,e)=>window.TestModes(args[1]);
   if(args.Length>0 && args[0]=="--playback-test") window.Loaded+=(s,e)=>window.TestPlayback(args[1]);
   app.Run(window); return 0;
  } catch(Exception ex) {if(args.Length>0) {File.WriteAllText(Path.Combine(Path.GetDirectoryName(Self),"vault-error.txt"),ex.ToString());} else MessageBox.Show(ex.Message,"Music Vault"); return 1;}
 }
}
partial class Player : Window {
 List<Song> songs, visible; MediaPlayer player=new MediaPlayer(); ListBox list=new ListBox(); TextBox search=new TextBox();
 TextBlock title=new TextBlock(), info=new TextBlock(), clock=new TextBlock(), count=new TextBlock(); Button play=new Button();
 Slider seek=new Slider(); CheckBox shuffle=new CheckBox(), repeat=new CheckBox(); Song current; bool playing,dragging; int version; Random random=new Random();
 string cache=Path.Combine(Path.GetTempPath(),"MusicVault-"+Guid.NewGuid().ToString("N")); string extracted;
 bool loading;
 static readonly System.Threading.SemaphoreSlim processing=new System.Threading.SemaphoreSlim(1,1);
 SolidColorBrush muted=new SolidColorBrush(Color.FromRgb(158,170,192));
 public Player() { InitializeRetro(); }
 Button Button(string label,RoutedEventHandler action) {var b=new Button {Content=label,Padding=new Thickness(16,9,16,9),Margin=new Thickness(0,0,8,0),Background=new SolidColorBrush(Color.FromRgb(116,210,190)),Foreground=new SolidColorBrush(Color.FromRgb(15,33,34)),BorderThickness=new Thickness(0),Cursor=System.Windows.Input.Cursors.Hand};b.Click+=action;return b;}
 string Time(TimeSpan t) {return ((int)t.TotalMinutes)+":"+t.Seconds.ToString("00");}
 public async void Smoke(string folder,string requestedSong=null) {
  try {
   if(songs.Count==0){
    Toggle(null,null);Next(1);Next(-1);search.Text="test";search.Text="";UpdateLayout();await Task.Delay(200);
    if(empty.Visibility!=Visibility.Visible || Icon==null)throw new Exception("Empty state or icon missing");
    var shot=new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);shot.Render(this);
    var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(shot));using(var output=File.Create(Path.Combine(folder,"empty-preview.png")))png.Save(output);
    File.WriteAllText(Path.Combine(folder,"empty-smoke.txt"),"PASS: zero tracks, icon loaded, empty-state rendering, search, and empty transport controls.");Close();return;
   }
   player.Volume=0;
   search.Text=songs[0].Name; if(visible.Count==0)throw new Exception("Search failed");search.Text="";
   Start(requestedSong==null?visible[0]:visible.First(x=>x.Name.IndexOf(requestedSong,StringComparison.OrdinalIgnoreCase)>=0));await WaitReady();
   await Task.Delay(700);if(player.Position.TotalSeconds<=0)throw new Exception("Playback did not advance");
   Toggle(null,null);if(playing)throw new Exception("Pause failed");seek.Value=15;Toggle(null,null);await Task.Delay(500);if(player.Position.TotalSeconds<14)throw new Exception("Seek failed");
   volume.Value=.3;if(Math.Abs(player.Volume-.3)>.01)throw new Exception("Volume failed");volume.Value=0;
   ToggleNight();ToggleMini();UpdateLayout();await Task.Delay(200);
   if(!playing || ActualWidth!=360 || ActualHeight!=360)throw new Exception("Mini mode interrupted playback");
   Mini<Slider>("MiniSeek").Value=20;await Task.Delay(200);if(player.Position.TotalSeconds<19)throw new Exception("Mini seeking failed");
   Mini<Button>("MiniPlay").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));if(playing)throw new Exception("Mini pause failed");Mini<Button>("MiniPlay").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
   CaptureMode(folder,"mini-playing");ToggleMini();ToggleNight();
   search.Text="no_matching_track_93840";if(visible.Count!=0 || empty.Visibility!=Visibility.Visible)throw new Exception("Empty search failed");search.Text="";UpdateLayout();await Task.Delay(250);
   var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(this);
   var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var output=File.Create(Path.Combine(folder,"preview.png")))encoder.Save(output);
   Width=MinWidth;Height=MinHeight;UpdateLayout();await Task.Delay(150);
   bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(this);encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var output=File.Create(Path.Combine(folder,"preview-compact.png")))encoder.Save(output);
   File.WriteAllText(Path.Combine(folder,"smoke.txt"),"PASS: full and compact window rendering, search and empty state, embedded MP3 playback, pause/resume, slider seeking, and volume.");
  }catch(Exception ex){File.WriteAllText(Path.Combine(folder,"smoke.txt"),"FAIL: "+ex);}
  Close();
 }
 void Add(object sender,RoutedEventArgs e) {
  var dialog=new OpenFileDialog {Title="Add songs inside Music Vault",Multiselect=true,Filter="Audio files|*.mp3;*.wav;*.m4a;*.wma;*.aac"};if(dialog.ShowDialog()!=true)return;
  try {
   string helper=Path.Combine(Path.GetTempPath(),"MusicVault-Updater-"+Guid.NewGuid().ToString("N")+".exe");
   string manifest=Path.Combine(Path.GetDirectoryName(Vault.Self),".music-import-"+Guid.NewGuid().ToString("N")+".txt");
   // Only the small executable stub is needed for the updater.
   long stub=songs.Count>0?songs.Min(x=>x.Offset):new FileInfo(Vault.Self).Length;
   using(var source=File.OpenRead(Vault.Self))using(var dest=File.Create(helper)){byte[] b=new byte[65536];while(stub>0){int n=source.Read(b,0,(int)Math.Min(stub,b.Length));if(n==0)throw new EndOfStreamException();dest.Write(b,0,n);stub-=n;}}
   File.WriteAllLines(manifest,dialog.FileNames.Select(x=>Convert.ToBase64String(Encoding.UTF8.GetBytes(x))),Encoding.UTF8);
   Process.Start(helper,"--update "+Vault.Quote(Vault.Self)+" "+Vault.Quote(manifest)+" "+Process.GetCurrentProcess().Id);
   Close();
  } catch(Exception ex){MessageBox.Show("Could not start the import.\n\n"+ex.Message,"Music Vault");}
 }
}
}
