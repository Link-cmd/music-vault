using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using NAudio.Wave;
namespace MusicVault {
static class AutoLevel {
 const string Compressor="aformat=sample_rates=48000:channel_layouts=stereo,acompressor=threshold=0.125:ratio=3:attack=10:release=300:makeup=1:knee=2.828:link=maximum:detection=rms";
 const string Target="loudnorm=I=-24:TP=-9:LRA=7";
 static string Number(double value){return value.ToString("0.######",CultureInfo.InvariantCulture);}
 static bool Finite(double value){return !double.IsNaN(value) && !double.IsInfinity(value);}
 static Dictionary<string,double> Measurements(string log){
  int start=log.LastIndexOf('{'),end=log.LastIndexOf('}');
  if(start<0 || end<=start)throw new InvalidDataException("Unable to measure this song's loudness.");
  var result=new Dictionary<string,double>();
  foreach(Match match in Regex.Matches(log.Substring(start,end-start+1),"\"([^\"]+)\"\\s*:\\s*\"([^\"]+)\"")){
   double value;string text=match.Groups[2].Value;
   if(text=="-inf")value=double.NegativeInfinity;else if(text=="inf")value=double.PositiveInfinity;
   else if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value))continue;
   result[match.Groups[1].Value]=value;
  }
  foreach(string key in new[]{"input_i","input_tp","input_lra","input_thresh","target_offset"})if(!result.ContainsKey(key))throw new InvalidDataException("Incomplete loudness measurements.");
  return result;
 }
 static string Run(string decoder,string arguments,CancellationToken cancel){
  cancel.ThrowIfCancellationRequested();
  var start=new ProcessStartInfo(decoder,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
  using(var process=Process.Start(start)){
   var errors=process.StandardError.ReadToEndAsync();var timer=Stopwatch.StartNew();
   while(!process.WaitForExit(100))if(cancel.IsCancellationRequested || timer.Elapsed.TotalMinutes>10){
    try{process.Kill();process.WaitForExit();}catch{}
    cancel.ThrowIfCancellationRequested();throw new InvalidDataException("Audio preparation timed out.");
   }
   string log=errors.GetAwaiter().GetResult();cancel.ThrowIfCancellationRequested();
   if(process.ExitCode!=0)throw new InvalidDataException("Unable to decode or balance this audio file.");
   return log;
  }
 }
 public static void Prepare(string source,string output,CancellationToken cancel){
  try{
   string decoder=Path.Combine(Path.GetDirectoryName(output),"vault-decoder.exe");
   if(!File.Exists(decoder)){
    using(var input=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("decoder.exe"))using(var file=File.Create(decoder)){
     if(input==null)throw new InvalidDataException("The audio decoder is missing.");input.CopyTo(file);
    }
   }
   string inputArgs="-hide_banner -nostats -loglevel info -nostdin -y -err_detect ignore_err -i "+Vault.Quote(source)+" -map 0:a:0 -vn -sn -dn ";
   var measured=Measurements(Run(decoder,inputArgs+"-af "+Vault.Quote(Compressor+","+Target+":print_format=json")+" -f null NUL",cancel));
   string filters=Compressor;
   // Frequency-weighted complete-track measurement excludes silence. A -24 LUFS
   // normalization stage followed by at least 6 dB attenuation aims at -30 LUFS.
   double attenuation=-6;
   if(Finite(measured["input_i"]) && Finite(measured["input_tp"]) && Finite(measured["target_offset"])){
    filters+=","+Target+":measured_I="+Number(measured["input_i"])+":measured_TP="+Number(measured["input_tp"])+":measured_LRA="+Number(measured["input_lra"])+":measured_thresh="+Number(measured["input_thresh"])+":offset="+Number(measured["target_offset"])+":linear=true:print_format=none";
    // Cap net upward gain at +6 dB; do not aggressively boost quiet recordings.
    attenuation=Math.Min(-6,measured["input_i"]+30);
   }
   // Keep make-up gain off so the final peak limiter cannot undo the attenuation.
   filters+=",volume="+Number(attenuation)+"dB,aresample=48000,alimiter=limit=0.125:attack=5:release=200:level=false:latency=true";
   Run(decoder,inputArgs+"-af "+Vault.Quote(filters)+" -ar 48000 -ac 2 -c:a pcm_s16le "+Vault.Quote(output),cancel);
   using(var wave=new WaveFileReader(output))if(wave.Length==0)throw new InvalidDataException("This file contains no recoverable audio.");
  }catch{try{File.Delete(output);}catch{}throw;}
 }
}
}
