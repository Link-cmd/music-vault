using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
namespace MusicVault {
partial class Player {
 class Prepared { public CancellationTokenSource Cancel=new CancellationTokenSource(); public Task<string> Work; public long Used; }
 readonly Dictionary<string,Prepared> prepared=new Dictionary<string,Prepared>();
 readonly List<Task<string>> preparationJobs=new List<Task<string>>();
 bool wantsPlay,mediaReady,closing; long useOrder; Song queuedNext; int preparationsStarted;
 Task<string> PrepareTrack(Song song) {
  Prepared item;
  if(prepared.TryGetValue(song.Hash,out item) && !item.Cancel.IsCancellationRequested && !item.Work.IsFaulted && !item.Work.IsCanceled){item.Used=++useOrder;return item.Work;}
  item=new Prepared {Used=++useOrder};var cancel=item.Cancel.Token;preparationsStarted++;
  item.Work=Task.Run(()=>{
   string source=Path.Combine(cache,Guid.NewGuid().ToString("N")+song.Extension),output=source+".wav";
   processing.Wait(cancel);
   try {cancel.ThrowIfCancellationRequested();Directory.CreateDirectory(cache);using(var dest=File.Create(source))if(Vault.CopySong(Vault.Self,song,dest)!=song.Hash)throw new InvalidDataException("Song integrity check failed.");AutoLevel.Prepare(source,output,cancel);cancel.ThrowIfCancellationRequested();return output;}
   catch {try{File.Delete(output);}catch{}throw;}
   finally {try{File.Delete(source);}catch{}processing.Release();}
  });preparationJobs.Add(item.Work);prepared[song.Hash]=item;return item.Work;
 }
 void CancelOtherJobs(Song song) {foreach(var entry in prepared)if(entry.Key!=song.Hash && !entry.Value.Work.IsCompleted)entry.Value.Cancel.Cancel();}
 void TrimPrepared() {
  foreach(var entry in prepared.OrderByDescending(x=>x.Value.Used).Skip(3).ToArray()) {
   if(entry.Key==(current==null?null:current.Hash) || entry.Key==(queuedNext==null?null:queuedNext.Hash) || !entry.Value.Work.IsCompleted)continue;
   if(entry.Value.Work.Status==TaskStatus.RanToCompletion)try{File.Delete(entry.Value.Work.Result);}catch{}
   prepared.Remove(entry.Key);
  }
 }
 bool Active(MediaPlayer media,int token){return !closing && token==version && ReferenceEquals(player,media);}
 async void Start(Song song) {
  if(closing || song==null)return;
  if(current==song && loading)return;
  if(current==song && mediaReady){player.Position=TimeSpan.Zero;wantsPlay=true;player.Play();playing=true;return;}
  CancelOtherJobs(song);int token=++version;wantsPlay=true;playing=false;loading=true;mediaReady=false;
  double level=player.Volume;player.Close();var media=new MediaPlayer {Volume=level};player=media;
  current=song;queuedNext=null;extracted=null;list.SelectedItem=song;list.ScrollIntoView(song);title.Text=song.Name;title.ToolTip=song.Name;info.Text="Balancing volume…";
  changingSeek=true;seek.Value=0;seek.IsEnabled=false;changingSeek=false;
  media.MediaOpened+=(s,e)=>{if(!Active(media,token))return;loading=false;mediaReady=true;if(media.NaturalDuration.HasTimeSpan){changingSeek=true;seek.Maximum=media.NaturalDuration.TimeSpan.TotalSeconds;seek.Value=0;changingSeek=false;seek.IsEnabled=true;}info.Text=wantsPlay?"STEREO / "+song.Format+" / AUTO LEVEL":"Paused";if(wantsPlay){media.Play();playing=true;}ScheduleNext();};
  media.MediaEnded+=(s,e)=>{if(!Active(media,token) || !wantsPlay)return;if(repeat.IsChecked==true){media.Position=TimeSpan.Zero;media.Play();}else Next(1);};
  media.MediaFailed+=(s,e)=>{if(!Active(media,token))return;loading=false;playing=false;mediaReady=false;wantsPlay=false;info.Text="Unable to play this track. Press Play to retry.";};
  try {string path=await PrepareTrack(song);if(!Active(media,token))return;extracted=path;media.Open(new Uri(path));TrimPrepared();}
  catch(OperationCanceledException){}
  catch(Exception ex){if(Active(media,token)){loading=false;playing=false;wantsPlay=false;info.Text="Unable to prepare this song: "+ex.Message;}}
 }
 Song ChooseNext(int direction) {
  var queue=visible.Count>0?visible:songs;if(queue.Count==0)return null;int i=queue.IndexOf(current),next=i<0?(direction>0?0:queue.Count-1):(i+direction+queue.Count)%queue.Count;
  if(shuffle.IsChecked==true && queue.Count>1){do{next=random.Next(queue.Count);}while(next==i);}return queue[next];
 }
 async void ScheduleNext() {
  if(closing || !mediaReady || repeat.IsChecked==true)return;
  var song=ChooseNext(1);queuedNext=song;if(song==null)return;
  try {await PrepareTrack(song);if(!closing)TrimPrepared();}catch(OperationCanceledException){}catch{} // A failed preload is retried when selected.
 }
 void Next(int direction) {var queue=visible.Count>0?visible:songs;var song=direction>0 && queuedNext!=null && queue.Contains(queuedNext)?queuedNext:ChooseNext(direction);if(song!=null)Start(song);}
 void Toggle(object sender,RoutedEventArgs e) {
  if(current==null){if(visible.Count>0)Start(list.SelectedItem as Song ?? visible[0]);return;}
  if(loading){wantsPlay=!wantsPlay;info.Text=wantsPlay?"Balancing volume…":"Balancing volume… playback paused";return;}
  if(!mediaReady){Start(current);return;}
  wantsPlay=!playing;if(wantsPlay)player.Play();else player.Pause();playing=wantsPlay;info.Text=playing?"STEREO / AUTO LEVEL":"Paused";
 }
 void ClosePlayback() {
  closing=true;version++;foreach(var item in prepared.Values)item.Cancel.Cancel();player.Close();
  Task.WhenAll(preparationJobs.ToArray()).ContinueWith(t=>{var ignored=t.Exception;try{Directory.Delete(cache,true);}catch{}});
 }
 async Task WaitReady(){for(int i=0;i<1200;i++){if(mediaReady)return;await Task.Delay(50);}throw new Exception("Playback preparation timed out: "+info.Text);}
 public async void TestPlayback(string folder) {
  try {
   player.Volume=0;Start(songs[0]);Toggle(null,null);await WaitReady();await Task.Delay(300);if(playing || player.Position.TotalSeconds>.1)throw new Exception("Pause during preparation was ignored");Toggle(null,null);await Task.Delay(500);if(!playing || player.Position.TotalSeconds<=0)throw new Exception("Resume failed");
   Start(songs[1]);Start(songs[0]);await WaitReady();await Task.Delay(500);if(current!=songs[0] || !playing)throw new Exception("Rapid switching failed");
   Start(songs[1]);await WaitReady();var first=prepared[songs[0].Hash].Work;var second=prepared[songs[1].Hash].Work;Start(songs[0]);await WaitReady();Start(songs[1]);await WaitReady();if(prepared[songs[0].Hash].Work!=first || prepared[songs[1].Hash].Work!=second)throw new Exception("Cached songs were prepared again");
   repeat.IsChecked=true;player.Position=player.NaturalDuration.TimeSpan-TimeSpan.FromMilliseconds(300);await Task.Delay(1100);if(current!=songs[1] || !playing || player.Position.TotalSeconds>2)throw new Exception("Repeat failed");
   repeat.IsChecked=false;player.Position=player.NaturalDuration.TimeSpan-TimeSpan.FromMilliseconds(300);await Task.Delay(1500);await WaitReady();if(current!=songs[2] || !playing)throw new Exception("Automatic next failed");
   File.WriteAllText(Path.Combine(folder,"playback-test.txt"),"PASS: pause during preparation, resume, rapid switching, cached replay, repeat, and automatic next track.");
  }catch(Exception ex){File.WriteAllText(Path.Combine(folder,"playback-test.txt"),"FAIL: "+ex);}Close();
 }
}
}
