#!/bin/zsh
# Quick look at buildings: Mac build + 7 screenshots.  ./homes_test.sh DIR [hour] [nobuild]
cd ~/CODE/gad-22x/UnityProject
U=/Applications/Unity/Hub/Editor/6000.3.21f1/Unity.app/Contents/MacOS/Unity
if [ "$3" != "nobuild" ]; then $U -batchmode -nographics -projectPath "$PWD" -executeMethod BuildScript.BuildMac -quit -logFile ~/CODE/gad-22x/buildmac.log || true; grep -E "error CS" ~/CODE/gad-22x/buildmac.log | sort -u | head; fi
SH=${1:A}; [[ $1 = /* ]] || SH=~/CODE/gad-22x/$1; rm -rf $SH; mkdir -p $SH
"./Builds/Mac/GAD22X.app/Contents/MacOS/Herbal" -autotest -homes -hour ${2:-15.5} -shots $SH -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile $SH/player.log >/dev/null 2>&1 || true
cd /private/tmp && python3 - $SH <<'PY'
import sys, glob
from PIL import Image
fs=sorted(glob.glob(sys.argv[1]+"/7*.png")); W,H=800,450
sh=Image.new("RGB",(W*2,H*((len(fs)+1)//2)))
for i,f in enumerate(fs): sh.paste(Image.open(f).convert("RGB").resize((W,H)),((i%2)*W,(i//2)*H))
sh.save(sys.argv[1]+"/sheet.jpg",quality=85)
PY
