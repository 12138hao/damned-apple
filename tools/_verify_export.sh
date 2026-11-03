#!/usr/bin/env bash
# 临时把主场景改成 underground，导出一个验证包，跑一遍确认第二关能从包里正常加载。
# 无论成功失败都会把 project.godot 还原成 firstry，并删掉验证包。
set -u

GODOT="D:/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
PROJ="D:/h/my-game"
EXE="$PROJ/build/_verify_underground.exe"

restore() {
  cd "$PROJ" || exit 1
  python -c "
import io
p='project.godot'
s=io.open(p,encoding='utf-8').read()
s=s.replace('run/main_scene=\"res://underground.tscn\"','run/main_scene=\"uid://c5cw7k2nfpe4s\"')
io.open(p,'w',encoding='utf-8',newline='').write(s)
"
  python -c "
import os
p='build/_verify_underground.exe'
if os.path.exists(p): os.remove(p); print('已删除验证包')
"
  echo "=== 已还原 main_scene ==="
  grep -n "main_scene" "$PROJ/project.godot"
}
trap restore EXIT

cd "$PROJ" || exit 1
python -c "
import io
p='project.godot'
s=io.open(p,encoding='utf-8').read()
s=s.replace('run/main_scene=\"uid://c5cw7k2nfpe4s\"','run/main_scene=\"res://underground.tscn\"')
io.open(p,'w',encoding='utf-8',newline='').write(s)
"
grep -n "main_scene" project.godot

env -u ELECTRON_RUN_AS_NODE -u NODE_OPTIONS "$GODOT" --headless --path "$PROJ" \
  --export-release "Windows Desktop" "$EXE" 2>&1 | tail -3

echo "=== 运行验证包（主场景=underground） ==="
cd "$PROJ/build" || exit 1
env -u ELECTRON_RUN_AS_NODE -u NODE_OPTIONS ./_verify_underground.exe --headless --quit-after 120 2>&1 \
  | grep -viE "ObjectDB|resources still in use|leaked|RID allocations" | head -30
