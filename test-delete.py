import pathlib,struct,subprocess,tempfile,shutil,hashlib
root=pathlib.Path(__file__).resolve().parent
exe=root/'MusicVault-base.exe'
def read(path):
    def string(f):
        n=0;shift=0
        while True:
            b=f.read(1)[0];n|=(b&127)<<shift
            if b<128:break
            shift+=7
        return f.read(n).decode('utf8')
    with path.open('rb') as f:
        f.seek(-16,2);pos=struct.unpack('<q',f.read(8))[0];assert f.read(8)==b'MVLT0001';f.seek(pos)
        songs=[]
        for _ in range(struct.unpack('<i',f.read(4))[0]):
            name=string(f);ext=string(f);offset,length=struct.unpack('<qq',f.read(16));digest=string(f);songs.append((name,offset,length,digest))
        for name,offset,length,digest in songs:
            f.seek(offset);assert hashlib.sha256(f.read(length)).hexdigest().upper()==digest
        return songs
with tempfile.TemporaryDirectory(prefix='delete-test-',dir=root) as tmp:
    folder=pathlib.Path(tmp);source=folder/'songs';source.mkdir()
    for name in ('quiet','loud'):shutil.copy(root/'leveling-tests'/(name+'.wav'),source)
    original=folder/'original.exe';one=folder/'one.exe';empty=folder/'empty.exe';again=folder/'again.exe'
    subprocess.run([str(exe),'--pack',str(original),str(source)],check=True)
    songs=read(original);assert len(songs)==2
    renamed=folder/'renamed.exe'
    subprocess.run([str(exe),'--rename-copy',str(original),str(renamed),songs[0][3],'  My Song — 夜  '],check=True)
    edited=read(renamed);assert len(edited)==2 and edited[0][0]=='My Song — 夜'
    assert [x[3] for x in edited]==[x[3] for x in songs],'Audio changed during rename'
    badname=folder/'blank-name.exe'
    p=subprocess.run([str(exe),'--rename-copy',str(original),str(badname),songs[0][3],'   '])
    assert p.returncode!=0 and not badname.exists(),'Blank name accepted'
    subprocess.run([str(exe),'--remove-copy',str(original),str(one),songs[0][3]],check=True)
    remaining=read(one);assert len(remaining)==1 and remaining[0][3]==songs[1][3]
    assert one.stat().st_size<original.stat().st_size,'Deleted audio was not reclaimed'
    assert len(read(original))==2,'Original changed during rebuild'
    subprocess.run([str(exe),'--remove-copy',str(one),str(empty),remaining[0][3]],check=True)
    assert read(empty)==[]
    # The actual empty output can still add music using its own embedded updater code.
    subprocess.run([str(empty),'--pack',str(again),str(source)],check=True)
    assert len(read(again))==2
    bad=folder/'bad.exe'
    p=subprocess.run([str(exe),'--remove-copy',str(one),str(bad),'missing-song'])
    assert p.returncode!=0 and not bad.exists()
report='PASS: rename including Unicode, whitespace trimming, blank-name rejection, original audio preserved; selected song removed; remaining bytes verified; space reclaimed; original preserved; last-song deletion; adding after emptying; invalid selection rejected.'
(root/'delete-test-results.txt').write_text(report)
print(report)
