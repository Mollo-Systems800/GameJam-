"""Original, deterministic office-themed score and Foley. No downloaded samples.
Run with the bundled Python (NumPy). Writes 24 kHz PCM WAVs into Assets/05_Sounds.
"""
from pathlib import Path
import json
import wave
import numpy as np

RATE = 24000
ROOT = Path(__file__).resolve().parents[1] / 'Assets' / '05_Sounds'
RNG = np.random.default_rng(261018)
REPORT = []

def tone(hz, seconds, kind='key'):
    t = np.arange(int(seconds * RATE)) / RATE
    attack = np.minimum(1, t / .008)
    release = np.minimum(1, (seconds-t) / .035)
    if kind == 'bass':
        s = np.sin(2*np.pi*hz*t) + .18*np.sin(4*np.pi*hz*t)
        env = np.exp(-t*4)
    elif kind == 'bell':
        s = np.sin(2*np.pi*hz*t) + .32*np.sin(2*np.pi*hz*2.76*t)*np.exp(-t*7)
        env = np.exp(-t*5)
    elif kind == 'strings':
        s = np.sin(2*np.pi*hz*t)+.18*np.sin(2*np.pi*hz*2.003*t)
        env = np.minimum(1,t/.12)*np.minimum(1,(seconds-t)/.2)*.5
    else:
        # Felt electric piano: gentle FM attack, warm fundamental, quiet tine.
        s = np.sin(2*np.pi*hz*t + .7*np.sin(2*np.pi*hz*2*t)*np.exp(-t*10))
        s += .14*np.sin(2*np.pi*hz*3*t)*np.exp(-t*8)
        env = np.exp(-t*3.2)
    return s * env * attack * release

def noise(seconds, decay=12, smooth=1):
    t=np.arange(int(seconds*RATE))/RATE
    x=RNG.normal(0,1,len(t))
    if smooth>1:x=np.convolve(x,np.ones(smooth)/smooth,mode='same')
    return x*np.exp(-decay*t)*np.minimum(1,t/.003)*np.minimum(1,(seconds-t)/.012)

def mix(buf, signal, at, gain=1, pan=0):
    start=int(at*RATE)
    if start>=len(buf):return
    signal=signal[:len(buf)-start]*gain
    if buf.ndim==1:buf[start:start+len(signal)]+=signal
    else:
        buf[start:start+len(signal),0]+=signal*np.sqrt((1-pan)/2)
        buf[start:start+len(signal),1]+=signal*np.sqrt((1+pan)/2)

def write(name, buf, music=False, ceiling=None):
    # Fixed ceiling, DC removal and short edge ramps. Loops have matching silent seams.
    buf=buf-np.mean(buf,axis=0)
    if music:
        dry=buf.copy()
        for delay,gain in [(.093,.11),(.187,.075),(.311,.035)]:
            n=int(delay*RATE);buf[n:]+=dry[:-n,::-1]*gain
    ramp=min(int((.025 if music else .006)*RATE),len(buf)//4)
    shape=np.linspace(0,1,ramp)
    if buf.ndim==2:shape=shape[:,None]
    buf[:ramp]*=shape;buf[-ramp:]*=shape[::-1]
    peak=float(np.max(np.abs(buf)))
    if peak>0:buf*= (ceiling if ceiling is not None else .62 if music else .78)/peak
    data=(buf*32767).astype('<i2')
    folder=ROOT/('Music' if music else 'Foley');folder.mkdir(parents=True,exist_ok=True)
    path=folder/(name+'.wav')
    with wave.open(str(path),'wb') as out:
        out.setnchannels(2 if buf.ndim==2 else 1);out.setsampwidth(2);out.setframerate(RATE);out.writeframes(data.tobytes())
    REPORT.append(dict(name=name,seconds=round(len(buf)/RATE,3),peak=round(float(np.max(np.abs(buf))),3),rms=round(float(np.sqrt(np.mean(buf**2))),4),channels=2 if music else 1))

def foley(name, style):
    b=np.zeros(int(1.6*RATE))
    if style=='step':
        mix(b,noise(.13,25,6),0,.65);mix(b,tone(93+RNG.uniform(-8,8),.17,'bass'),.012,.6)
        mix(b,noise(.08,35,2),.05,.14)
    elif style=='paper':
        for at in [0,.04,.13,.22]:mix(b,noise(.2,11,3),at,.2)
    elif style=='stamp':
        mix(b,tone(104,.22,'bass'),0,.7);mix(b,noise(.14,35,2),.006,.3);mix(b,tone(420,.12,'bell'),.065,.08)
    elif style=='printer':
        for at in [0,.045,.09,.135]:mix(b,noise(.07,35,2),at,.22)
        t=np.arange(int(.28*RATE))/RATE
        mix(b,np.sin(2*np.pi*(220*t+250*t*t))*np.sin(np.pi*t/.28)**2,.1,.18)
        mix(b,noise(.25,9,4),.28,.35)
    elif style=='roll':
        mix(b,noise(.65,4,6),0,.5)
        for at in [.12,.4,.7]:mix(b,tone(85,.18,'bass'),at,.35)
    elif style=='breath':
        t=np.arange(int(1.2*RATE))/RATE
        x=RNG.normal(0,1,len(t));x=np.convolve(x,np.ones(24)/24,mode='same')
        mix(b,x*np.sin(np.pi*t/1.2)**2,0,.3)
    elif style=='hurt':
        mix(b,noise(.3,12,8),0,.6)
        t=np.arange(int(.4*RATE))/RATE
        mix(b,np.sin(2*np.pi*(145*t-90*t*t))*np.exp(-t*12),0,.55)
    elif style=='metal':
        for f in [175,391,687,1051]:mix(b,tone(f,.45,'bell'),0,.2)
        mix(b,noise(.1,25,2),0,.1)
    elif style=='jump':
        mix(b,noise(.22,12,7),0,.5)
        t=np.arange(int(.2*RATE))/RATE
        mix(b,np.sin(2*np.pi*(180*t+530*t*t))*np.sin(np.pi*t/.2)**2,0,.15)
    elif style=='deny':
        for at,f in [(0,155),(.15,137)]:mix(b,tone(f,.25,'key'),at,.4)
    elif style=='tick':
        mix(b,noise(.045,60,2),0,.5);mix(b,tone(810,.05,'bell'),0,.2)
    else:
        notes={'coin':[880,1320], 'heal':[293.66,369.99,440,587.33], 'shield':[220,330,440,660],
               'speed':[293.66,440,587.33,880], 'double':[440,587.33,880],
               'win':[293.66,369.99,440,587.33], 'loss':[293.66,261.63,220],
               'alert':[220,233.08,220], 'expire':[440,330,293.66], 'ui':[440,660]}.get(style,[293.66,440])
        for i,f in enumerate(notes):mix(b,tone(f,.45,'bell'),i*.11,.35)
        if style=='coin':mix(b,noise(.07,40,2),0,.08)
    end=np.flatnonzero(np.abs(b)>1e-5)
    b=b[:min(len(b),int(end[-1]+RATE*.06))] if len(end) else b
    write(name,b)

def score(name,bpm,bars,mode):
    beat=60/bpm;length=bars*4*beat;b=np.zeros((int(length*RATE),2))
    # D minor office tango; the final resolves to D major.
    chords=[[50,57,62,65],[46,53,58,62],[48,55,60,64],[45,52,57,61]]
    if mode=='victory':chords=[[50,57,62,66],[55,59,62,67],[57,61,64,69],[50,57,62,66]]
    melody=[74,69,72,69,65,69,62,0,74,77,76,72,69,65,64,0]
    for bar in range(bars):
        chord=chords[bar%4];at=bar*4*beat
        for q in [0,2]:mix(b,tone(440*2**((chord[0]-69)/12),beat*.8,'bass'),at+q*beat,.29,-.1)
        for q in ([1,3] if mode in ['menu','normal','area','victory'] else [.5,1.5,2.5,3.5]):
            for midi in chord[1:]:mix(b,tone(440*2**((midi-69)/12),beat*.8,'key'),at+q*beat,.085,.25)
        for step in range(8):
            tick=noise(.055,65,2)
            mix(b,tick,at+step*beat/2,.055 if mode!='threat' else .105,(-.4 if step%2 else .4))
        for q in range(4):
            midi=melody[(bar*4+q)%len(melody)]
            if not midi:continue
            if mode in ['threat','capture']:midi=[74,75,69,68][(q+bar)%4]
            if mode=='health':midi=melody[(15-q-bar)%len(melody)] or 62
            if mode=='victory' and midi==65:midi=66
            mix(b,tone(440*2**((midi-69)/12),beat*1.25,'bell' if mode in ['menu','victory','area'] else 'key'),at+(q+.05)*beat,.11,-.25)
        if mode in ['threat','capture','fear']:
            for q in [0,.28,1,1.28,2,2.28,3,3.28]:
                mix(b,tone(62,.15,'bass'),at+q*beat,.18,0)
            for midi in [50,57]:mix(b,tone(440*2**((midi-69)/12),4*beat,'strings'),at,.055,0)
        if mode in ['normal','menu']:
            # Ventilation and soft office machinery under the composition.
            x=noise(4*beat,.12,32)
            mix(b,x,at,.035,.1)
        if mode=='health':
            for midi in chord:mix(b,tone(440*2**((midi-69)/12),3*beat,'strings'),at,.05,0)
    write(name,b,True)

def band_noise(seconds, low, high):
    n=int(seconds*RATE)
    spectrum=np.fft.rfft(RNG.normal(0,1,n))
    hz=np.fft.rfftfreq(n,1/RATE)
    spectrum*=np.clip((hz-low)/max(1,low*.3),0,1)*np.clip((high-hz)/max(1,high*.3),0,1)
    x=np.fft.irfft(spectrum,n)
    return x/max(1e-8,np.std(x))

def calm_office(name='OfficeNormal', bpm=112, menu=False):
    beat=60/bpm;bars=16;length=bars*4*beat
    b=np.zeros((int(length*RATE),2))
    chords=[[62,65,69,76],[58,62,65,72],[60,64,67,74],[57,61,64,71]]
    # A quicker, restrained felt-piano ostinato; no alarm or bass drone.
    for bar in range(bars):
        chord=chords[bar%4];start=bar*4*beat
        for i,q in enumerate([0,.75,1.5,2.5,3.25]):
            midi=chord[(bar+i)%len(chord)]
            mix(b,tone(440*2**((midi-69)/12),beat*1.5,'key'),start+q*beat,.11,-.28 if i%2 else .28)
        for midi in chord[:3]:
            mix(b,tone(440*2**((midi-69)/12),beat*3.5,'strings'),start,.025,0)
        for q in [0,2]:
            mix(b,tone(220*2**((bar%3-1)/12),beat*.6,'bass'),start+q*beat,.035,0)
        for i in range(8):
            mix(b,noise(.045,70,8),start+i*beat/2,.006,(-.35 if i%2 else .35))
        if menu and bar%2==0:
            mix(b,tone(880*2**((bar%4)/12),beat*1.6,'bell'),start+3*beat,.025,-.15)
    write(name,b,True,.46 if menu else .34)

def oppressive_boss(name='OfficeThreat',bpm=148,bars=16):
    beat=60/bpm;length=bars*4*beat
    t=np.arange(int(length*RATE))/RATE
    # This is a separate industrial horror cue, not a faster version of the calm melody.
    drone=(np.sin(2*np.pi*36.708*t)+.65*np.sin(2*np.pi*37.06*t)+.40*np.sin(2*np.pi*55.062*t))
    drone+=.18*np.sin(2*np.pi*77.78*t+.5*np.sin(2*np.pi*.18*t))
    grit=band_noise(length,28,230)
    bed=np.tanh(drone*1.1+grit*.18)*(.21+.035*np.sin(2*np.pi*.31*t))
    # Audible upper harmonics keep the menace present on small laptop speakers too.
    bed+=.10*np.sin(2*np.pi*220.25*t+.2*np.sin(2*np.pi*.27*t))+.055*np.sin(2*np.pi*233.08*t)
    b=np.column_stack([bed,bed])
    scrape=band_noise(length,420,1800)*(.008+.008*np.sin(2*np.pi*.23*t)**2)
    b[:,0]+=scrape;b[:,1]+=np.roll(scrape,int(.03*RATE))
    for bar in range(bars):
        at=bar*4*beat
        for q in [0,.32,1,1.32,2,2.32,3,3.32]:
            seconds=.23;u=np.arange(int(seconds*RATE))/RATE
            kick=np.sin(2*np.pi*(38*u+44*(1-np.exp(-u*20))/20))*np.exp(-u*18)
            mix(b,kick,at+q*beat,.32 if q%1==0 else .16,0)
        if bar%4==0:
            u=np.arange(int(1.9*RATE))/RATE
            horn=(np.sin(2*np.pi*(55*u-4*u*u))+.34*np.sin(2*np.pi*(110*u-8*u*u)))
            horn=np.tanh(horn*2)*np.minimum(1,u/.1)*np.exp(-u*1.8)
            mix(b,horn,at,.32,-.2 if bar%8 else .2)
        if bar%2==1:
            u=np.arange(int(.5*RATE))/RATE
            mix(b,band_noise(.5,300,2000)*np.sin(np.pi*u/.5)**2,at+3*beat,.035,.5 if bar%4==1 else -.5)
    write(name,b,True,.72)

def low_warning(name):
    t=np.arange(int(1.05*RATE))/RATE
    hit=np.sin(2*np.pi*(42*t+58*(1-np.exp(-t*7))/7))
    hit+=.38*np.sin(2*np.pi*55*t)
    hit=np.tanh(hit*2)*np.exp(-t*4)*np.minimum(1,t/.012)
    hit+=band_noise(1.05,60,650)*np.exp(-t*8)*.13*np.minimum(1,t/.01)
    write(name,hit)

def main():
    for i in range(1,5):foley('Footstep'+str(i),'step')
    clips={'Jump':'jump','DoubleJump':'double','Land':'step','Roll':'roll','Breath':'breath','SprintBreath':'breath',
           'Hurt':'hurt','ShieldHit':'metal','Coin':'coin','Pills':'paper','Heal':'heal','Shield':'shield','Speed':'speed',
           'DoubleJumpPower':'double','PowerExpired':'expire','UIHover':'tick','UIClick':'stamp','ShopOpen':'paper','ShopClose':'paper',
           'Purchase':'coin','Denied':'deny','PrinterFire':'printer','PaperImpact':'paper','ShovelMove':'metal','ShovelHit':'hurt',
           'BossRustle':'paper','BossAlert':'alert','BossAccelerate':'alert','LevelStart':'ui','LevelWin':'win','AreaWin':'win',
           'Victory':'win','DefeatBoss':'stamp','DefeatHealth':'loss','PageTurn':'paper','Stamp':'stamp','RetryTick':'tick','NewMaze':'paper'}
    for name,style in clips.items():foley(name,style)
    calm_office('Menu',116,True);calm_office();oppressive_boss()
    low_warning('BossAlert');low_warning('BossAccelerate')
    oppressive_boss('StoryBossLoss',136,4)
    for args in [('StoryIntro',116,4,'fear'),('StoryArea',112,4,'area'),('StoryVictory',108,5,'victory'),
                 ('StoryHealthLoss',90,3,'health')]:score(*args)
    report=Path(__file__).resolve().parents[1]/'Logs'/'office-audio-assets.json'
    report.write_text(json.dumps(REPORT,indent=2))
    print(f'Created {len(REPORT)} original clips; peak <= 0.78; {sum(x["seconds"] for x in REPORT):.1f} seconds.')

if __name__=='__main__':
    import sys
    if '--horror-update' in sys.argv:
        calm_office('Menu',116,True);calm_office();oppressive_boss();oppressive_boss('StoryBossLoss',136,4)
        low_warning('BossAlert');low_warning('BossAccelerate')
        (Path(__file__).resolve().parents[1]/'Logs'/'office-audio-horror-assets.json').write_text(json.dumps(REPORT,indent=2))
        print('Recomposed calm/menu/industrial horror tracks and deep boss warnings.')
    else:main()
