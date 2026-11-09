from pathlib import Path
import sys, subprocess, json, wave
import numpy as np
from PIL import Image, ImageDraw, ImageFont

root=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root/'tmp'/'video-tools'))
import imageio_ffmpeg
out=root/'Submission'
frames=out/'VideoFrames'
files=sorted(frames.glob('frame_*.jpg'))
assert len(files)==800, f'Expected 800 captured frames, found {len(files)}'

# A quiet original synth bed matching the in-world Harbor FM motif.
rate=22050
t=np.arange(int(40*rate),dtype=np.float64)/rate
beat=60/112
beats=t/beat
roots=np.array([45,41,48,43])[(beats//4).astype(int)%4]
interval=np.array([0,7,12,7,3,7,15,7])[(beats*2).astype(int)%8]
minor=((beats//4).astype(int)%4)==0
interval=np.where((interval==3)&~minor,4,interval)
interval=np.where((interval==15)&~minor,16,interval)
bassfreq=440*2.0**((roots-69)/12.0)
arpeggio=440*2.0**((roots+12+interval-69)/12.0)
phase=beats%1
step=(beats*2)%1
sound=.09*np.sin(2*np.pi*t*bassfreq)*np.exp(-phase*2)
sound+=.08*np.sin(2*np.pi*t*arpeggio)*np.exp(-step*5)
sound+=.085*np.sin(phase*(52+75*np.exp(-phase*22))*2*np.pi)*np.exp(-phase*17)
sound*=np.minimum(1,t/1.3)*np.minimum(1,(40-t)/2)
audio=out/'HarborFM_Original.wav'
with wave.open(str(audio),'wb') as w:
    w.setparams((1,2,rate,len(sound),'NONE','not compressed'))
    w.writeframes((np.clip(sound,-1,1)*32767).astype('<i2').tobytes())

encoder=imageio_ffmpeg.get_ffmpeg_exe()
video=out/'PalmHarbor_Walkthrough.mp4'
command=[encoder,'-y','-framerate','20','-i',str(frames/'frame_%05d.jpg'),'-i',str(audio),
         '-vf','fade=t=in:st=0:d=0.6,fade=t=out:st=39:d=1','-c:v','libx264','-preset','medium',
         '-crf','19','-pix_fmt','yuv420p','-c:a','aac','-b:a','128k','-t','40','-movflags','+faststart',str(video)]
subprocess.run(command,check=True,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)

# Inspect one frame from each shot and retain a compact review contact sheet.
sheet=Image.new('RGB',(960,910),(15,23,35))
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18)
draw=ImageDraw.Draw(sheet)
for i,index in enumerate([30,170,300,440,570,730]):
    im=Image.open(files[index]).convert('RGB');im.thumbnail((474,266))
    x=(i%2)*480+3;y=(i//2)*300+3
    sheet.paste(im,(x,y));draw.text((x+8,y+270),f'Shot {i+1}  |  {index/20:.1f}s',font=font,fill=(220,238,242))
sheet.save(out/'Video_Review.jpg',quality=92)
print(json.dumps({'video':str(video),'frames':len(files),'durationSeconds':40,'resolution':'1280x720','sizeBytes':video.stat().st_size}))
