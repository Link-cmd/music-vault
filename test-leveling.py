import array, math, pathlib, subprocess, wave
root=pathlib.Path(__file__).resolve().parent
folder=root/'leveling-tests';folder.mkdir(exist_ok=True)
exe=root/'MusicVault-base.exe'
def make(name, amplitude, spike=False, silence=False, burst=False):
    path=folder/(name+'.wav');values=array.array('h')
    for i in range(48000*3):
        x=0 if silence else amplitude*math.sin(2*math.pi*440*i/48000)
        if burst and 48000<=i<62400:x=.95*math.sin(2*math.pi*440*i/48000)
        if spike and i==48000:x=.99
        values.extend([round(x*32767),round(x*.5*32767)])
    with wave.open(str(path),'wb') as f:f.setparams((2,2,48000,0,'NONE',''));f.writeframes(values.tobytes())
    output=folder/(name+'-balanced.wav')
    subprocess.run([str(exe),'--level',str(path),str(output)],check=True)
    with wave.open(str(output),'rb') as f:
        assert f.getnframes()==48000*3
        data=array.array('h',f.readframes(f.getnframes()))
    rms=math.sqrt(sum((x/32768)**2 for x in data)/len(data))
    peak=max(abs(x) for x in data)/32768
    assert peak<=.2501,(name,peak)
    if burst:
        section=data[48000*2:62400*2]
        burst_rms=math.sqrt(sum((x/32768)**2 for x in section)/len(section))
        assert burst_rms<.075,burst_rms
    assert max(abs(data[i]*.5-data[i+1]) for i in range(0,len(data),2))<=6, 'Stereo balance changed'
    return rms,peak
loud=make('loud',.9);quiet=make('quiet',.04);spike=make('spike',.04,True);silence=make('silence',0,silence=True)
assert abs(20*math.log10(loud[0]/quiet[0]))<.2,(loud,quiet)
assert silence==(0,0),silence
assert .24<spike[1]<=.2501,spike
assert loud[0]<.051,loud
make('quiet-with-loud-burst',.02,burst=True)
report=f'PASS: loud/quiet difference {abs(20*math.log10(loud[0]/quiet[0])):.3f} dB; peak {spike[1]:.4f} <= 0.25; loud passages controlled; silence stays silent; stereo balance and duration preserved.\n'
(folder/'results.txt').write_text(report)
print(report)
