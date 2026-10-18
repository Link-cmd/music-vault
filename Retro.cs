using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MusicVault {
static partial class Vault {
 // Copy from the current vault, including songs imported after the first build.
 public static void Rebuild(string original,string output,string selectedHash=null,string renameTo=null) {
  var songs=Read(original);
  if(selectedHash!=null){var selected=songs.FirstOrDefault(x=>x.Hash==selectedHash);if(selected==null)throw new InvalidDataException("The selected song is no longer in this collection.");if(renameTo==null)songs=songs.Where(x=>x.Hash!=selectedHash).ToList();else {renameTo=renameTo.Trim();if(renameTo.Length==0 || renameTo.Length>300 || renameTo.Any(char.IsControl))throw new InvalidDataException("Enter a name of 1–300 characters without control characters.");selected.Name=renameTo;}}
  using(var dest=new FileStream(output,FileMode.CreateNew,FileAccess.Write)) {
   var embedded=Read(Self);long stubLength=embedded.Count>0?embedded.Min(x=>x.Offset):new FileInfo(Self).Length;
   using(var stub=File.OpenRead(Self)){byte[] buffer=new byte[65536];while(stubLength>0){int n=stub.Read(buffer,0,(int)Math.Min(buffer.Length,stubLength));if(n==0)throw new EndOfStreamException();dest.Write(buffer,0,n);stubLength-=n;}}
   foreach(var song in songs) {long offset=dest.Position;if(CopySong(original,song,dest)!=song.Hash)throw new InvalidDataException("Song verification failed: "+song.Name);song.Offset=offset;}
   long index=dest.Position;
   using(var writer=new BinaryWriter(dest,Encoding.UTF8,true)) {
    writer.Write(songs.Count);foreach(var song in songs){writer.Write(song.Name);writer.Write(song.Extension);writer.Write(song.Offset);writer.Write(song.Length);writer.Write(song.Hash);}writer.Write(index);writer.Write(Magic);writer.Flush();
   }dest.Flush(true);
  }
  if(Read(output).Count!=songs.Count)throw new InvalidDataException("Collection verification failed.");
 }
}
partial class Player {
 Grid ui; OrbitDisplay orbit=new OrbitDisplay(); TextBlock deckState, trackNumber, results, empty, hint, libraryCount;
 Slider volume; bool changingSeek;
 T Find<T>(string name) where T:FrameworkElement {return (T)ui.FindName(name);}
 void InitializeRetro() {
  Title="Music Vault · Compact Edition"; Width=780;Height=510;MinWidth=720;MinHeight=480;
  Width=Math.Min(Width,SystemParameters.WorkArea.Width-30);Height=Math.Min(Height,SystemParameters.WorkArea.Height-30);
  WindowStartupLocation=WindowStartupLocation.CenterScreen; WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResizeWithGrip;
  Background=new SolidColorBrush(Color.FromRgb(250,250,248));Foreground=new SolidColorBrush(Color.FromRgb(36,38,36));FontFamily=new FontFamily("Consolas");FontSize=13;
  UseLayoutRounding=true;SnapsToDevicePixels=true;
  using(var iconStream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("vault.ico"))Icon=System.Windows.Media.Imaging.BitmapFrame.Create(iconStream,System.Windows.Media.Imaging.BitmapCreateOptions.None,System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
  System.Windows.Shell.WindowChrome.SetWindowChrome(this,new System.Windows.Shell.WindowChrome {CaptionHeight=0,ResizeBorderThickness=new Thickness(5),CornerRadius=new CornerRadius(0),GlassFrameThickness=new Thickness(0)});
  songs=Vault.Read(Vault.Self).OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ToList();for(int i=0;i<songs.Count;i++)songs[i].Number=(i+1).ToString("000");Directory.CreateDirectory(cache);
  using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Retro.xaml"))ui=(Grid)System.Windows.Markup.XamlReader.Load(stream);Content=ui;
  list=Find<ListBox>("SongList");search=Find<TextBox>("SearchBox");title=Find<TextBlock>("SongTitle");info=Find<TextBlock>("StatusText");clock=Find<TextBlock>("ClockText");count=Find<TextBlock>("CollectionInfo");play=Find<Button>("PlayButton");seek=Find<Slider>("SeekSlider");shuffle=Find<CheckBox>("ShuffleBox");repeat=Find<CheckBox>("RepeatBox");volume=Find<Slider>("VolumeSlider");
  deckState=Find<TextBlock>("DeckState");trackNumber=Find<TextBlock>("TrackNumber");results=Find<TextBlock>("ResultsText");empty=Find<TextBlock>("EmptyText");hint=Find<TextBlock>("SearchHint");libraryCount=Find<TextBlock>("LibraryCount");
  Find<ContentControl>("OrbitHost").Content=orbit;
  Find<Button>("CloseButton").Click+=(s,e)=>Close();Find<Button>("MinimizeButton").Click+=(s,e)=>WindowState=WindowState.Minimized;Find<Button>("MaximizeButton").Click+=(s,e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
  Find<Grid>("DragBar").MouseLeftButtonDown+=(s,e)=>{for(var node=e.OriginalSource as DependencyObject;node!=null && node!=s;node=VisualTreeHelper.GetParent(node))if(node is System.Windows.Controls.Primitives.ButtonBase)return;if(e.ClickCount==2)WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;else DragMove();};
  Find<Button>("AddButton").Click+=Add;Find<Button>("PreviousButton").Click+=(s,e)=>Next(-1);Find<Button>("NextButton").Click+=(s,e)=>Next(1);play.Click+=Toggle;
  search.TextChanged+=(s,e)=>Filter();
  list.MouseDoubleClick+=(s,e)=>{for(var n=e.OriginalSource as DependencyObject;n!=null && n!=list;n=VisualTreeHelper.GetParent(n))if(n is System.Windows.Controls.Primitives.ButtonBase)return;if(list.SelectedItem is Song)Start((Song)list.SelectedItem);};list.KeyDown+=(s,e)=>{if(e.Key==Key.Enter && list.SelectedItem is Song){Start((Song)list.SelectedItem);e.Handled=true;}};
  seek.PreviewMouseLeftButtonDown+=(s,e)=>dragging=true;
  seek.PreviewMouseLeftButtonUp+=(s,e)=>{if(player.NaturalDuration.HasTimeSpan)player.Position=TimeSpan.FromSeconds(seek.Value);dragging=false;};
  seek.LostMouseCapture+=(s,e)=>{if(dragging && player.NaturalDuration.HasTimeSpan)player.Position=TimeSpan.FromSeconds(seek.Value);dragging=false;};
  seek.ValueChanged+=(s,e)=>{if(!changingSeek && !dragging && player.NaturalDuration.HasTimeSpan)player.Position=TimeSpan.FromSeconds(seek.Value);};
  volume.ValueChanged+=(s,e)=>{player.Volume=volume.Value;Find<TextBlock>("VolumeText").Text=((int)(volume.Value*100)).ToString("00");};player.Volume=.4;
  player.MediaOpened+=(s,e)=>{if(player.NaturalDuration.HasTimeSpan){seek.Maximum=player.NaturalDuration.TimeSpan.TotalSeconds;seek.IsEnabled=true;}info.Text="STEREO / "+current.Format+" / AUTO LEVEL";};
  player.MediaEnded+=(s,e)=>{if(repeat.IsChecked==true){player.Position=TimeSpan.Zero;player.Play();}else Next(1);};
  player.MediaFailed+=(s,e)=>{playing=false;play.Content="▶ PLAY";info.Text="Unable to play this track. Select another.";};
  var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(50)};
  timer.Tick+=(s,e)=>{
   orbit.Animate(playing && !mini && WindowState!=WindowState.Minimized);miniOrbit.Progress=orbit.Progress;miniOrbit.Animate(playing && mini && WindowState!=WindowState.Minimized);
   play.Content=playing?"Ⅱ PAUSE":"▶ PLAY";deckState.Text=loading?"◌ PREPARING":playing?"● PLAYING":current==null?"● STANDBY":"Ⅱ PAUSED";
   if(current!=null){trackNumber.Text=current.Number+" / "+songs.Count.ToString("000");if(player.NaturalDuration.HasTimeSpan){changingSeek=true;if(!dragging)seek.Value=player.Position.TotalSeconds;changingSeek=false;clock.Text=Time(player.Position)+" / "+Time(player.NaturalDuration.TimeSpan);orbit.Progress=player.Position.TotalSeconds/Math.Max(1,player.NaturalDuration.TimeSpan.TotalSeconds);}}
  };timer.Start();
  Closed+=(s,e)=>{version++;if(preparation!=null)preparation.Cancel();timer.Stop();player.Close();try{Directory.Delete(cache,true);}catch{}};
  PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Space && !(Keyboard.FocusedElement is TextBox) && !(Keyboard.FocusedElement is Button) && !(Keyboard.FocusedElement is CheckBox)){Toggle(null,null);e.Handled=true;}if(e.Key==Key.F && Keyboard.Modifiers==ModifierKeys.Control){search.Focus();search.SelectAll();e.Handled=true;}};
  SetupModes();SetupDelete();Filter();
 }
 void Filter() {
  visible=songs.Where(x=>x.Name.IndexOf(search.Text,StringComparison.CurrentCultureIgnoreCase)>=0).ToList();list.ItemsSource=visible;
  count.Text=(songs.Sum(x=>x.Length)/1000000000.0).ToString("0.00")+" GB / ON BOARD ●";
  libraryCount.Text=songs.Count.ToString("000")+" TRACKS";results.Text=visible.Count+" / "+songs.Count;
  hint.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;empty.Visibility=visible.Count==0?Visibility.Visible:Visibility.Collapsed;
  empty.Text=songs.Count==0?"YOUR COLLECTION STARTS HERE.\n\nClick + ADD SONGS to load your music.":"NO SIGNAL FOUND.\nTry another search.";
  if(songs.Count==0){title.Text="A fresh start.";info.Text="ADD YOUR FIRST TRACK TO BEGIN.";}
  if(current!=null)list.SelectedItem=current;
 }
}
class OrbitDisplay : FrameworkElement {
 double angle;bool active;public double Progress;
 public bool Night; Brush gold {get{return new SolidColorBrush(Night?Color.FromRgb(213,224,216):Color.FromRgb(42,48,43));}} Brush dim {get{return new SolidColorBrush(Night?Color.FromRgb(100,120,108):Color.FromRgb(151,161,152));}} Brush orange {get{return new SolidColorBrush(Night?Color.FromRgb(162,199,173):Color.FromRgb(72,97,78));}}
 public void Animate(bool playing){if(playing)angle+=.012;if(playing||active!=playing){active=playing;InvalidateVisual();}}
 void Text(DrawingContext d,string value,double x,double y,Brush brush,double size) {d.DrawText(new FormattedText(value,System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Consolas"),size,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip),new Point(x,y));}
 protected override void OnRender(DrawingContext d) {
  base.OnRender(d);double w=ActualWidth,h=ActualHeight;if(w<50||h<50)return;
  double cx=w*.5,cy=h*.46,r=Math.Min(w*.31,h*.29);var center=new Point(cx,cy);
  var faint=new SolidColorBrush(Color.FromArgb(35,100,110,100));
  for(double x=17;x<w;x+=20)for(double y=18;y<h-25;y+=20)d.DrawRectangle(faint,null,new Rect(x,y,1,1));
  for(double y=0;y<h;y+=4)d.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(5,0,0,0)),1),new Point(0,y),new Point(w,y));
  Text(d,"A / SIDE",16,15,gold,10);Text(d,"33⅓",w-48,15,gold,12);
  Text(d,"LONG PLAY",16,31,dim,9);Text(d,"STEREO",w-65,32,dim,9);
  d.DrawLine(new Pen(dim,1),new Point(16,cy),new Point(cx-r-18,cy));d.DrawLine(new Pen(dim,1),new Point(cx+r+18,cy),new Point(w-16,cy));
  d.DrawLine(new Pen(dim,1),new Point(cx,cy-r-25),new Point(cx,cy-r-8));d.DrawLine(new Pen(dim,1),new Point(cx,cy+r+8),new Point(cx,cy+r+24));
  for(int i=0;i<72;i++){double a=i*Math.PI/36;double length=i%6==0?8:3;d.DrawLine(new Pen(i<Progress*72?orange:dim,1),new Point(cx+Math.Cos(a)*(r+11),cy+Math.Sin(a)*(r+11)),new Point(cx+Math.Cos(a)*(r+11+length),cy+Math.Sin(a)*(r+11+length)));}
  d.PushTransform(new RotateTransform(-23,cx,cy));d.DrawEllipse(null,new Pen(dim,1),center,r*1.35,r*.42);d.Pop();
  d.DrawEllipse(new SolidColorBrush(Night?Color.FromRgb(28,33,31):Color.FromRgb(244,245,242)),new Pen(gold,1.2),center,r,r);
  // Dotted grooves give the disc the printed texture of an old technical plate.
  for(int ring=0;ring<17;ring++){
   double rad=r*(.28+ring*.04);int dots=(int)(rad*2.7);
   for(int j=0;j<dots;j++){double a=j*Math.PI*2/dots+angle*(ring%2==0?1:.7);double light=Math.Sin(a*2+ring*.27);if(light>-.35 || j%3==0){double size=light>.75?1.7:1;d.DrawRectangle(light>.3?gold:dim,null,new Rect(cx+Math.Cos(a)*rad,cy+Math.Sin(a)*rad,size,size));}}
  }
  d.DrawEllipse(gold,null,center,r*.23,r*.23);d.DrawEllipse(null,new Pen(new SolidColorBrush(Color.FromRgb(36,38,36)),1),center,r*.17,r*.17);d.DrawEllipse(new SolidColorBrush(Night?Color.FromRgb(28,33,31):Color.FromRgb(244,245,242)),null,center,4,4);
  d.PushTransform(new RotateTransform(angle*180/Math.PI,cx,cy));d.DrawLine(new Pen(orange,2),new Point(cx,cy-r*.94),new Point(cx,cy-r*.71));d.Pop();
  // An orbital satellite follows elapsed playback; it is a motion motif, not a spectrum meter.
  double satellite=angle*.65;d.DrawEllipse(orange,null,new Point(cx+Math.Cos(satellite)*r*1.23,cy+Math.Sin(satellite)*r*.58),4,4);
  d.DrawLine(new Pen(dim,1),new Point(cx+r*.68,cy-r*.7),new Point(w-39,cy-r-25));d.DrawRectangle(null,new Pen(gold,1),new Rect(w-42,cy-r-28,6,6));
 }
}
}
