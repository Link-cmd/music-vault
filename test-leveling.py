import array,json,math,pathlib,re,subprocess,wave
root=pathlib.Path(__file__).resolve().parent
folder=root/'leveling-tests';folder.mkdir(exist_ok=True)
exe=root/'MusicVault-base.exe'
decoder=root/'.deps/decoder-package/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
rate=48000
def run(args):
    result=subprocess.run([str(x) for x in args],capture_output=True,text=True)
    assert result.returncode==0,result.stderr[-1500:]
    return result.stderr
def measure(path,start=None,duration=None):
    args=[decoder,'-hide_banner','-nostats']
    if start is not None:args+=['-ss',str(start)]
    args+=['-i',path]
    if duration is not None:args+=['-t',str(duration)]
    log=run(args+['-af','loudnorm=print_format=json','-f','null','-'])
    return {k:float(v) for k,v in json.loads(log[log.rfind('{'):log.rfind('}')+1]).items() if k.startswith('input_')}
def create(name,amp=.2,freq=1000,intro=False,burst=False):
    source=folder/(name+'.wav');out=folder/(name+'-balanced.wav');values=array.array('h')
    for i in range(rate*12):
        t=i/rate;amplitude=0 if intro and t<4 else amp
        if burst and 4<=t<6:amplitude=.8
        sample=round(amplitude*math.sin(2*math.pi*freq*t)*32767)
        values.extend((sample,sample))
    with wave.open(str(source),'wb') as f:f.setparams((2,2,rate,0,'NONE',''));f.writeframes(values.tobytes())
    run([exe,'--level',source,out])
    before=measure(source);after=measure(out)
    with wave.open(str(out),'rb') as f:
        assert abs(f.getnframes()/f.getframerate()-12)<.025,'Duration changed'
        samples=array.array('h',f.readframes(f.getnframes()))
    peak=max(abs(x) for x in samples)/32768
    assert peak<=.1251,(name,peak)
    assert after['input_tp']<=-17.7,(name,after)
    assert after['input_i']<=-29.0,(name,after)
    if math.isfinite(before['input_i']):assert after['input_i']-before['input_i']<=6.5,(name,before,after)
    if amp==0:assert peak==0,'Silence was amplified'
    print(f'{name}: {before["input_i"]} -> {after["input_i"]} LUFS, {after["input_tp"]} dBTP',flush=True)
    return after
results={}
for name,args in [('loud',{'amp':.8}),('quiet',{'amp':.02}),('bass',{'freq':80}),('bright',{'freq':4000}),('intro',{'amp':.1,'intro':True}),('burst',{'amp':.025,'burst':True}),('silence',{'amp':0}),('very-quiet',{'amp':.0001})]:results[name]=create(name,**args)
assert abs(results['loud']['input_i']-results['quiet']['input_i'])<.6,'Loud/quiet mismatch'
assert abs(results['bass']['input_i']-results['bright']['input_i'])<.8,'Frequency weighting mismatch'
burst=measure(folder/'burst-balanced.wav',4.2,1.4)
assert burst['input_i']<=-23,'Loud burst not controlled'
(folder/'results.json').write_text(json.dumps(results,indent=2))
print('PASS: perceptual matching, quieter target, true/sample peaks, boost cap, loud passage, silence, and duration.')
