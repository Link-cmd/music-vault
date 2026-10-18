using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace MusicVault {
partial class Player {
 Button FindRowAction(DependencyObject parent,string tag){if(parent==null)return null;var button=parent as Button;if(button!=null && (string)button.Tag==tag)return button;for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var found=FindRowAction(VisualTreeHelper.GetChild(parent,i),tag);if(found!=null)return found;}return null;}
 void SetupDelete(){
  list.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,new RoutedEventHandler((s,e)=>{var button=e.OriginalSource as Button;if(button==null || !(button.DataContext is Song))return;string action=button.Tag as string;if(action!="rename-song" && action!="delete-song")return;list.SelectedItem=button.DataContext;e.Handled=true;if(action=="rename-song")RenameSong(button,e);else DeleteSong(button,e);}));
  var menu=new ContextMenu();var item=new MenuItem{Header="Delete song…"};var renameItem=new MenuItem{Header="Rename song…"};renameItem.Click+=RenameSong;menu.Items.Add(renameItem);item.Click+=DeleteSong;menu.Items.Add(item);menu.Opened+=(s,e)=>{item.IsEnabled=list.SelectedItem is Song;renameItem.IsEnabled=item.IsEnabled;};list.ContextMenu=menu;
  list.PreviewMouseRightButtonDown+=(s,e)=>{var n=e.OriginalSource as DependencyObject;while(n!=null && !(n is ListBoxItem) && n!=list)n=VisualTreeHelper.GetParent(n);list.SelectedItem=n is ListBoxItem?((ListBoxItem)n).Content:null;};
  list.KeyDown+=(s,e)=>{if(e.Key==Key.Delete){e.Handled=true;DeleteSong(s,e);}if(e.Key==Key.F2){e.Handled=true;RenameSong(s,e);}};
 }
 void DeleteSong(object sender,RoutedEventArgs e){
  var song=list.SelectedItem as Song;if(song==null)return;
  if(MessageBox.Show(this,"Delete this song from Music Vault?\n\n"+song.Name+"\n\nThe app will save and restart. To restore this song later, you will need to add it again. Original files outside this app are not deleted.","Delete song",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
  ChangeSong(new[]{song.Hash},"--delete");
 }
 void RenameSong(object sender,RoutedEventArgs e){
  var song=list.SelectedItem as Song;if(song==null)return;
  var dialog=new Window{Owner=this,Title="Rename song",Width=460,Height=230,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Background,Foreground=Foreground,FontFamily=FontFamily,Resources=Resources};
  var panel=new StackPanel{Margin=new Thickness(20)};dialog.Content=panel;
  panel.Children.Add(new TextBlock{Text="SONG NAME",FontSize=12,Margin=new Thickness(0,0,0,10)});
  var input=new TextBox{Text=song.Name,MaxLength=300,Padding=new Thickness(7),FontSize=15,Background=(Brush)Resources["CFFFFFF"],Foreground=Foreground};panel.Children.Add(input);
  var note=new TextBlock{Text="The player saves and restarts after renaming.",FontSize=11,Margin=new Thickness(0,10,0,12),TextWrapping=TextWrapping.Wrap};panel.Children.Add(note);
  var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};panel.Children.Add(actions);
  var cancel=new Button{Content="CANCEL",IsCancel=true,Margin=new Thickness(0,0,8,0)};var save=new Button{Content="SAVE NAME",IsDefault=true};actions.Children.Add(cancel);actions.Children.Add(save);
  save.Click+=(s,args)=>{string value=input.Text.Trim();if(value.Length==0 || value.Any(char.IsControl)){note.Text="Please enter a song name without line breaks.";return;}dialog.DialogResult=true;};
  dialog.ContentRendered+=(s,args)=>{input.Focus();input.SelectAll();};
  if(dialog.ShowDialog()==true && input.Text.Trim()!=song.Name)ChangeSong(new[]{song.Hash,input.Text.Trim()},"--rename");
 }
 void ChangeSong(string[] values,string operation){
  try{
   string helper=Path.Combine(Path.GetTempPath(),"MusicVault-Delete-"+Guid.NewGuid().ToString("N")+".exe");
   string manifest=Path.Combine(Path.GetDirectoryName(Vault.Self),".music-delete-"+Guid.NewGuid().ToString("N")+".txt");
   long stub=songs.Min(x=>x.Offset);
   using(var source=File.OpenRead(Vault.Self))using(var dest=File.Create(helper)){byte[] b=new byte[65536];while(stub>0){int n=source.Read(b,0,(int)Math.Min(stub,b.Length));if(n==0)throw new EndOfStreamException();dest.Write(b,0,n);stub-=n;}}
   File.WriteAllLines(manifest,values.Select(x=>Convert.ToBase64String(Encoding.UTF8.GetBytes(x))),Encoding.UTF8);
   Process.Start(helper,operation+" "+Vault.Quote(Vault.Self)+" "+Vault.Quote(manifest)+" "+Process.GetCurrentProcess().Id);Close();
  }catch(Exception ex){MessageBox.Show(this,"Could not update the song.\n\n"+ex.Message,"Music Vault");}
 }
}
}
