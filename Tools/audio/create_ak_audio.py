"""Generate short ORIGINAL, procedural WAV audition drafts for Adventure King Reborn.
No copyrighted melodies, samples or soundfonts are used. These are not mastered music.
"""
import numpy as np, wave
from pathlib import Path
R=22050
O=Path('/mnt/data/AK_Original_Audio_Drafts');O.mkdir(exist_ok=True)

def hz(m): return 440*2**((m-69)/12)

def ADSR(n,attack=.025,decay=.12,sustain=.55,release=.4):
    att=min(n,max(1,int(attack*R)));dec=min(max(1,int(decay*R)),max(0,n-att));rel=min(max(1,int(release*R)),max(0,n-att-dec));sus=max(0,n-att-dec-rel)
    return np.r_[np.linspace(0,1,att,endpoint=False),np.linspace(1,sustain,dec,endpoint=False),np.full(sus,sustain),np.linspace(sustain,0,rel,endpoint=True)][:n]

def tone(note,dur,typ,amp):
    n=int(dur*R);t=np.arange(n,dtype='float32')/R;f=hz(note)
    if typ=='bell':
        v=(np.sin(2*np.pi*f*t)+.25*np.sin(2*np.pi*2.01*f*t)*np.exp(-4*t)+.17*np.sin(2*np.pi*3.95*f*t)*np.exp(-7*t))
        env=np.exp(-2.8*t)*np.minimum(1,t/.006)
    elif typ=='flute':
        v=np.sin(2*np.pi*f*t+.008*np.sin(2*np.pi*4.2*t))+.12*np.sin(2*np.pi*2*f*t)
        env=ADSR(n,.18,.3,.74,.38)
    elif typ=='pad':
        v=(np.sin(2*np.pi*f*t)+.27*np.sin(2*np.pi*2*f*t)+.13*np.sin(2*np.pi*3*f*t))
        v*=.9+.1*np.sin(2*np.pi*.35*t)
        env=ADSR(n,.7,.75,.6,1)
    elif typ=='pluck':
        v=(np.sin(2*np.pi*f*t)+.35*np.sin(2*np.pi*2*f*t)+.14*np.sin(2*np.pi*3*f*t))
        env=np.exp(-4.4*t)*np.minimum(1,t/.007)
    else:
        v=np.sin(2*np.pi*f*t);env=ADSR(n,.1,.2,.7,.2)
    return (v*env*amp).astype('float32')

def place(a, start, note, dur, typ, amp):
    snd=tone(note,dur,typ,amp);i=int(start*R);n=len(a);j=(np.arange(len(snd))+i)%n
    np.add.at(a,j,snd)

def write(path,y):
    # Circular early reflections preserve seamless repeats.
    y=y+.17*np.roll(y,int(.17*R))+.09*np.roll(y,int(.33*R))+.05*np.roll(y,int(.49*R))
    # Fold edges into each other, preserving looping ambience.
    N=min(len(y)//5,int(.14*R));u=np.linspace(0,1,N,endpoint=False)
    begin=y[:N].copy();end=y[-N:].copy();mid=(1-u)*end+u*begin
    y[-N:]=mid;y[:N]=mid
    y=np.tanh(y*1.1);m=max(.01,np.max(np.abs(y)));y=y*(.62/m)
    pcm=(np.clip(y,-1,1)*32767).astype('<i2')
    with wave.open(str(path),'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(R);w.writeframes(pcm.tobytes())

TRACKS=[
    ('BGM_Login_Original_Draft.wav',[(57,60,64),(53,57,60),(55,59,62),(57,60,64)],.25,'login'),
    ('BGM_Lobby_Day_Original_Draft.wav',[(55,59,62),(60,64,67),(57,61,64),(55,59,62)],.34,'day'),
    ('BGM_Lobby_Night_Original_Draft.wav',[(52,55,59),(48,52,55),(55,59,62),(50,54,57)],.20,'night'),
    ('BGM_Map_Adventure_Original_Draft.wav',[(57,61,64),(62,66,69),(64,68,71),(57,61,64)],.38,'adventure'),
    ('BGM_Boss_Original_Draft.wav',[(50,53,57),(48,52,55),(46,50,53),(45,49,52)],.42,'boss'),
    ('BGM_Wedding_Original_Draft.wav',[(60,64,67),(57,60,64),(65,69,72),(67,71,74)],.23,'wedding'),
]
for fname,chords,vol,role in TRACKS:
    dur=24.;a=np.zeros(int(dur*R),dtype='float32');
    for k,chord in enumerate(chords):
        start=k*6
        for i,n in enumerate(chord): place(a,start,n,6,'pad',vol*(.095 if role!='boss' else .12))
        bass=chord[0]-12
        for i in range(3 if role in ('day','boss','adventure') else 1):
            place(a,start+2*i,bass,1.5,'flute' if role=='night' else 'pad',vol*.10)
        for j in range(8):
            n=chord[j%3]+12*(j%4==3)
            if role in ('day','adventure','boss'):
                place(a,start+j*.75,n,.7,'pluck' if role!='boss' else 'bell',vol*.28)
            elif j%2==0:
                place(a,start+j*.75,n,.85,'bell',vol*.28)
        melody=[0,1,2,1,0,2]
        for j,ix in enumerate(melody):
            if role in ('night','wedding','login') and j%2:continue
            place(a,start+j, chord[ix]+12, 1.05 if role not in ('night','login') else 1.7,'flute',vol*(.17 if role!='boss' else .13))
    write(O/fname,a)
# Original UI confirmation stinger and gentle click, short mono wav
for name,notes in [('SFX_RewardClaim_Original_Draft.wav',[76,80,83,88]),('SFX_UI_Click_Original_Draft.wav',[76,83])]:
    dur=2.2 if 'Reward' in name else .24;a=np.zeros(int(dur*R),dtype='float32')
    for i,n in enumerate(notes):place(a,i*(.19 if 'Reward' in name else .06),n,.85 if 'Reward' in name else .13,'bell',.27)
    write(O/name,a)
print('Created:')
for p in O.iterdir():print(p.name, round(p.stat().st_size/1024/1024,2),'MiB')
