using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using NAudio.Wave;
namespace MusicVault {
static class AutoLevel {
 // Gated, track-wide RMS leveling. Not an EBU R128/true-peak implementation.
 const double TargetRms=.05, Ceiling=.25, PassageRms=.065; // -26 dBFS average; -12 dBFS sample-peak ceiling.
 public static void Prepare(string source,string output,CancellationToken cancel) {
  try {PrepareCore(source,output,cancel,false);return;}
  catch(OperationCanceledException){throw;}
  catch {cancel.ThrowIfCancellationRequested();}
  string repaired=output+".decoded.wav";
  try {
   string decoder=Path.Combine(Path.GetDirectoryName(output),"vault-decoder.exe");
   if(!File.Exists(decoder))using(var input=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("decoder.exe"))using(var file=File.Create(decoder))input.CopyTo(file);
   var start=new ProcessStartInfo(decoder,"-hide_banner -loglevel error -nostdin -y -err_detect ignore_err -i "+Vault.Quote(source)+" -map 0:a:0 -vn -ac 2 -ar 48000 -c:a pcm_s16le "+Vault.Quote(repaired)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
   using(var process=Process.Start(start)) {
    var errors=process.StandardError.ReadToEndAsync();var timer=Stopwatch.StartNew();
    while(!process.WaitForExit(100)){if(cancel.IsCancellationRequested || timer.Elapsed.TotalMinutes>5){try{process.Kill();process.WaitForExit();}catch{}cancel.ThrowIfCancellationRequested();throw new InvalidDataException("Decoding timed out.");}}
    string detail=errors.GetAwaiter().GetResult();
    if(process.ExitCode!=0 || !File.Exists(repaired) || new FileInfo(repaired).Length<100)throw new InvalidDataException("This file has no recoverable audio. "+detail.Substring(0,Math.Min(180,detail.Length)));
   }
   PrepareCore(repaired,output,cancel,true);
  }finally{try{File.Delete(repaired);}catch{}}
 }
 static void PrepareCore(string source,string output,CancellationToken cancel,bool decodedPcm) {
  string raw=output+".raw";
  try {
   var energy=new List<double>();var peaks=new List<double>();var sizes=new List<int>();int rate,channels,block;
   using(WaveStream decoded=decodedPcm?(WaveStream)new WaveFileReader(source):new MediaFoundationReader(source))using(var stream=File.Create(raw))using(var writer=new BinaryWriter(stream)) {
    var reader=new NAudio.Wave.SampleProviders.SampleChannel(decoded,false);
    rate=reader.WaveFormat.SampleRate;channels=reader.WaveFormat.Channels;block=Math.Max(1,rate/100)*channels;
    if(channels<1 || channels>2)throw new InvalidDataException("Automatic leveling supports mono and stereo audio.");
    float[] samples=new float[block];int n;
    while((n=reader.Read(samples,0,samples.Length))>0){cancel.ThrowIfCancellationRequested();double sum=0,peak=0;
     for(int j=0;j<n;j++){float v=samples[j];if(float.IsNaN(v)||float.IsInfinity(v))v=0;writer.Write(v);sum+=(double)v*v;peak=Math.Max(peak,Math.Abs(v));}
     energy.Add(sum/n);peaks.Add(peak);sizes.Add(n);
    }
   }
   if(energy.Count==0)throw new InvalidDataException("The decoder returned no audio.");
   // Gate 400 ms windows, so silence does not inflate the normalization gain.
   var windows=new List<double>();
   for(int i=0;i<energy.Count;i+=40){double sum=0;long n=0;for(int j=i;j<Math.Min(i+40,energy.Count);j++){sum+=energy[j]*sizes[j];n+=sizes[j];}if(n>0 && sum/n>1e-6)windows.Add(sum/n);}
   double mean=windows.Count>0?windows.Average():0;
   var gated=windows.Where(x=>x>=mean*.0316228).ToArray();if(gated.Length>0)mean=gated.Average();
   double gain=mean>1e-6?Math.Min(3.981072,TargetRms/Math.Sqrt(mean)):1; // Maximum +12 dB; silence is not boosted.
   // Catch loud passages inside otherwise quiet songs, not just whole-track averages.
   var passage=new double[energy.Count];
   for(int i=0;i<energy.Count;i++){double sum=0;long n=0;for(int j=Math.Max(0,i-5);j<Math.Min(energy.Count,i+5);j++){sum+=energy[j]*sizes[j];n+=sizes[j];}passage[i]=n>0?Math.Sqrt(sum/n)*gain:0;}
   using(var input=new BinaryReader(File.OpenRead(raw)))using(var wave=new WaveFileWriter(output,new WaveFormat(rate,16,channels))) {
    double previous=1;byte[] pcm=new byte[block*2];
    for(int i=0;i<peaks.Count;i++){
     cancel.ThrowIfCancellationRequested();double peak=Math.Max(peaks[i],i+1<peaks.Count?peaks[i+1]:0)*gain;
     double desired=peak>Ceiling?Ceiling/peak:1;
     if(passage[i]>PassageRms)desired=Math.Min(desired,PassageRms/passage[i]);
     if(i==0)previous=desired;
     double end=desired<previous?desired:previous+(desired-previous)*(1-Math.Exp(-.01/.4));
     int frames=sizes[i]/channels;
     for(int f=0;f<frames;f++){
      double envelope=previous+(end-previous)*(f+1)/(double)frames;
      for(int c=0;c<channels;c++){double value=input.ReadSingle()*gain*envelope;value=Math.Max(-Ceiling,Math.Min(Ceiling,value));short sample=(short)Math.Round(value*32767);int k=(f*channels+c)*2;pcm[k]=(byte)(sample&255);pcm[k+1]=(byte)((sample>>8)&255);}
     }
     wave.Write(pcm,0,frames*channels*2);previous=end;
    }
   }
  }catch{try{File.Delete(output);}catch{}throw;}
  finally{try{File.Delete(raw);}catch{}}
 }
}
}
