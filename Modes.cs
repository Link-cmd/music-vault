using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;
using System.Threading.Tasks;
namespace MusicVault {
partial class Player {
 bool night,mini;Grid miniUi;OrbitDisplay miniOrbit=new OrbitDisplay();double libraryWidth,libraryHeight;
 Dictionary<string,Color> dayColors=new Dictionary<string,Color>();
 T Mini<T>(string name) where T:FrameworkElement{return (T)miniUi.FindName(name);}
 void SetIcon(Button button,string geometry,string label,double size=16){
  var icon=new System.Windows.Shapes.Path{Data=Geometry.Parse(geometry),Stretch=Stretch.None,StrokeThickness=1.2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round};
  BindingOperations.SetBinding(icon,System.Windows.Shapes.Shape.StrokeProperty,new Binding("Foreground"){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(Button),1)});
  var canvas=new Canvas{Width=20,Height=20,ClipToBounds=false};Canvas.SetLeft(icon,2);Canvas.SetTop(icon,2);canvas.Children.Add(icon);
  button.Content=new Viewbox{Width=size+4,Height=size+4,Child=canvas};button.ToolTip=label;System.Windows.Automation.AutomationProperties.SetName(button,label);
 }
 void UpdateModeIcons(){
  string sun="M 11,8 A 3,3 0 1 1 5,8 A 3,3 0 1 1 11,8 M 8,0 L 8,2 M 8,14 L 8,16 M 0,8 L 2,8 M 14,8 L 16,8 M 2.3,2.3 L 3.7,3.7 M 12.3,12.3 L 13.7,13.7 M 2.3,13.7 L 3.7,12.3 M 12.3,3.7 L 13.7,2.3";
  string moon="M 13.5,11 A 6.5,6.5 0 1 1 5,2 A 5.5,5.5 0 0 0 13.5,11 Z";
  SetIcon(Find<Button>("NightButton"),night?sun:moon,night?"Switch to day mode":"Switch to night mode");
  SetIcon(Mini<Button>("MiniNight"),night?sun:moon,night?"Switch to day mode":"Switch to night mode",14);
  SetIcon(Find<Button>("MiniButton"),"M 2,1 L 14,1 L 14,15 L 2,15 Z","Open square MP3 player");
  SetIcon(Mini<Button>("ExpandMini"),"M 1,2 L 15,2 L 15,14 L 1,14 Z M 1,5 L 15,5 M 6,5 L 6,14","Return to full library",14);
 }
 void Bind(DependencyObject target,DependencyProperty property,object source,string path,bool twoWay=false){BindingOperations.SetBinding(target,property,new Binding(path){Source=source,Mode=twoWay?BindingMode.TwoWay:BindingMode.OneWay});}
 void SetupModes(){
  Resources=ui.Resources;
  foreach(object key in Resources.Keys)if(key is string && ((string)key).Length==7 && ((string)key).StartsWith("C") && Resources[key] is SolidColorBrush)dayColors[(string)key]=((SolidColorBrush)Resources[key]).Color;
  Find<Button>("NightButton").Click+=(s,e)=>ToggleNight();Find<Button>("MiniButton").Click+=(s,e)=>ToggleMini();
  using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Mini.xaml"))miniUi=(Grid)System.Windows.Markup.XamlReader.Load(stream);
  Mini<ContentControl>("MiniOrbit").Content=miniOrbit;
  Bind(Mini<TextBlock>("MiniTitle"),TextBlock.TextProperty,title,"Text");Bind(Mini<TextBlock>("MiniTitle"),FrameworkElement.ToolTipProperty,title,"Text");Bind(Mini<TextBlock>("MiniStatus"),TextBlock.TextProperty,info,"Text");Bind(Mini<TextBlock>("MiniClock"),TextBlock.TextProperty,clock,"Text");Bind(Mini<Button>("MiniPlay"),ContentControl.ContentProperty,play,"Content");
  Bind(Mini<Slider>("MiniVolume"),Slider.ValueProperty,volume,"Value",true);
  var smallSeek=Mini<Slider>("MiniSeek");Bind(smallSeek,Slider.MaximumProperty,seek,"Maximum");Bind(smallSeek,UIElement.IsEnabledProperty,seek,"IsEnabled");Bind(smallSeek,Slider.ValueProperty,seek,"Value",true);
  smallSeek.PreviewMouseLeftButtonDown+=(s,e)=>dragging=true;smallSeek.PreviewMouseLeftButtonUp+=(s,e)=>{if(player.NaturalDuration.HasTimeSpan)player.Position=TimeSpan.FromSeconds(smallSeek.Value);dragging=false;};
  smallSeek.LostMouseCapture+=(s,e)=>{if(dragging && player.NaturalDuration.HasTimeSpan)player.Position=TimeSpan.FromSeconds(smallSeek.Value);dragging=false;};
  Mini<Button>("MiniPlay").Click+=Toggle;Mini<Button>("MiniPrevious").Click+=(s,e)=>Next(-1);Mini<Button>("MiniNext").Click+=(s,e)=>Next(1);Mini<Button>("ExpandMini").Click+=(s,e)=>ToggleMini();Mini<Button>("MiniNight").Click+=(s,e)=>ToggleNight();Mini<Button>("CloseMini").Click+=(s,e)=>Close();
  Mini<Grid>("MiniDrag").MouseLeftButtonDown+=(s,e)=>{for(var n=e.OriginalSource as DependencyObject;n!=null && n!=s;n=VisualTreeHelper.GetParent(n))if(n is System.Windows.Controls.Primitives.ButtonBase)return;DragMove();};
  UpdateModeIcons();
 }
 void ToggleNight(){
  night=!night;
  foreach(var item in dayColors){Color c=item.Value;double l=(c.R+c.G+c.B)/3.0;byte v=(byte)(l>220?25+(255-l)*.45:l<80?232-l*.3:Math.Min(200,280-l));Resources[item.Key]=new SolidColorBrush(night?Color.FromRgb(v,(byte)Math.Min(255,v+4),(byte)Math.Min(255,v+2)):c);}
  Foreground=(Brush)Resources["C242624"];Background=(Brush)Resources["CFAFAF8"];
  orbit.Night=night;miniOrbit.Night=night;orbit.InvalidateVisual();miniOrbit.InvalidateVisual();
  UpdateModeIcons();
 }
 void ToggleMini(){
  if(!mini){if(WindowState!=WindowState.Normal)WindowState=WindowState.Normal;libraryWidth=Width;libraryHeight=Height;MinWidth=360;MinHeight=360;MaxWidth=360;MaxHeight=360;Width=360;Height=360;ResizeMode=ResizeMode.NoResize;Content=miniUi;mini=true;}
  else{MaxWidth=double.PositiveInfinity;MaxHeight=double.PositiveInfinity;MinWidth=720;MinHeight=480;Width=libraryWidth;Height=libraryHeight;ResizeMode=ResizeMode.CanResizeWithGrip;Content=ui;mini=false;Left=Math.Max(SystemParameters.WorkArea.Left,Math.Min(Left,SystemParameters.WorkArea.Right-Width));Top=Math.Max(SystemParameters.WorkArea.Top,Math.Min(Top,SystemParameters.WorkArea.Bottom-Height));}
 }
 void CaptureMode(string folder,string name){UpdateLayout();var shot=new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);shot.Render(this);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(shot));using(var output=File.Create(Path.Combine(folder,name+".png")))encoder.Save(output);}
 public async void TestModes(string folder){try{
  search.Text="In This Shirt";search.Focus();UpdateLayout();await Task.Delay(200);CaptureMode(folder,"search-fixed");
  Find<Button>("NightButton").Focus();CaptureMode(folder,"icons-focused-day");ToggleNight();await Task.Delay(150);CaptureMode(folder,"night-mode");
  if(visible.Count>0){
   list.SelectedItem=visible[0];list.ScrollIntoView(visible[0]);UpdateLayout();
   var dialogTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};
   dialogTimer.Tick+=(s,e)=>{dialogTimer.Stop();var dialog=Application.Current.Windows.Cast<Window>().FirstOrDefault(x=>x.Title=="Rename song");if(dialog!=null){dialog.UpdateLayout();var shot=new System.Windows.Media.Imaging.RenderTargetBitmap((int)dialog.ActualWidth,(int)dialog.ActualHeight,96,96,PixelFormats.Pbgra32);shot.Render(dialog);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(shot));using(var file=File.Create(Path.Combine(folder,"rename-dialog.png")))encoder.Save(file);dialog.DialogResult=false;}};
   var row=list.ItemContainerGenerator.ContainerFromItem(visible[0]);var edit=FindRowAction(row,"rename-song");var delete=FindRowAction(row,"delete-song");if(edit==null || delete==null)throw new Exception("Row action icons missing");
   list.SelectedItem=null;dialogTimer.Start();edit.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));if(list.SelectedItem!=visible[0])throw new Exception("Row pencil selected the wrong song");
  }
  ToggleMini();await Task.Delay(150);
  if(ActualWidth!=360 || ActualHeight!=360)throw new Exception("Mini mode is not square");Mini<Slider>("MiniVolume").Value=.2;if(Math.Abs(player.Volume-.2)>.001)throw new Exception("Mini volume is disconnected");CaptureMode(folder,"mini-night");
  ToggleNight();await Task.Delay(150);CaptureMode(folder,"mini-day");ToggleMini();if(search.Text!="In This Shirt")throw new Exception("Search not preserved");
  File.WriteAllText(Path.Combine(folder,"modes-test.txt"),"PASS: search rendering; night/day switching; 360 x 360 mini mode; volume binding; library and search restored.");
 }catch(Exception ex){File.WriteAllText(Path.Combine(folder,"modes-test.txt"),"FAIL: "+ex);}Close();}
}
}
