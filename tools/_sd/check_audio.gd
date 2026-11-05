extends SceneTree

# 检查 jungle / jungleinside 用到的所有音频文件齐不齐。
# 用法：Godot --headless --path . --script res://tools/_sd/check_audio.gd

const SFX := [
	"jump", "hurt", "death", "attack", "swing", "charge",
	"mud_splash", "mud_step",
	"apple_charge", "apple_swing", "apple_hurt", "apple_die",
	"apple_fire", "apple_grab", "apple_throw",
	"blip", "pickup",
]
const BGM := ["jungle", "jungle_inside"]
const EXTS := [".ogg", ".wav", ".mp3"]     # 按这个顺序优先


func _hit(dir: String, name: String) -> String:
	for e in EXTS:
		var p := "res://assets/audio/%s/%s%s" % [dir, name, e]
		if ResourceLoader.exists(p):
			return e
	return ""


func _initialize() -> void:
	var miss := 0
	print("=== 音效 assets/audio/sfx/ ===")
	for n in SFX:
		var e := _hit("sfx", n)
		if e == "":
			miss += 1
			print("  缺  %s" % n)
		else:
			print("  有  %-16s %s" % [n, e])
	print("")
	print("=== 背景音乐 assets/audio/bgm/ ===")
	for n in BGM:
		var e := _hit("bgm", n)
		if e == "":
			miss += 1
			print("  缺  %s" % n)
		else:
			print("  有  %-16s %s" % [n, e])
	print("")
	print("缺 %d 个 / 共 %d 个" % [miss, SFX.size() + BGM.size()])
	quit()
