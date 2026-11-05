extends SceneTree

# 用引擎自己的依赖图，确认「准备删的文件」没有被任何场景/资源引用
# 注意：Godot 4.7 的 get_dependencies() 返回的是字符串数组，格式 "uid://xxx::::res://path"
const TARGETS := [
	"res://ext_chunk_1.png", "res://ext_chunk_2.png", "res://ext_chunk_3.png",
	"res://ext_raw_1.png", "res://tmp_mud_cut.png",
	"res://jungle_map_10000x512.png", "res://jungle_scene_v2.png", "res://ground_big_cut.png",
	"res://assets/player_sheet.png", "res://assets/sky.png",
	"res://assets/pixel_cave_background.png", "res://assets/pixel_cave_brown_quartz.png",
	"res://assets/boss/poison_apple.png", "res://assets/boss/sheet_hat7.png",
	"res://assets/vehicle_icon.png",
	"res://assets/ui/boss/seedream-20261026-010332-01.jpg",
	"res://assets/ui/boss/seedream-20261026-010350-01.jpg",
	"res://assets/ui/boss/seedream-20261026-162228-01.jpg",
]

func _initialize() -> void:
	var roots: Array = []
	_scan("res://", roots)
	var deps := {}
	for r in roots:
		for d in ResourceLoader.get_dependencies(r):
			var s := str(d)
			var p := s.split("::::")
			deps[p[p.size() - 1]] = true
	print("依赖图共 %d 个资源，扫描了 %d 个场景/资源" % [deps.size(), roots.size()])
	var bad := 0
	for t in TARGETS:
		if deps.has(t):
			bad += 1
			print("  !! 被引用  %s" % t)
		else:
			print("  ok 未被引用  %s" % t)
	print("")
	print("结论: %s" % ("有 %d 个仍被引用，不能删！" % bad if bad > 0 else "全部 %d 个都可以安全删除" % TARGETS.size()))
	quit()

func _scan(dir: String, out: Array) -> void:
	var d := DirAccess.open(dir)
	if d == null: return
	d.list_dir_begin()
	var n := d.get_next()
	while n != "":
		if n.begins_with("."):
			n = d.get_next(); continue
		var p := dir.path_join(n)
		if d.current_is_dir():
			if n not in ["tools", "docs", "dsh-image-gen"]:
				_scan(p, out)
		else:
			var e := p.get_extension().to_lower()
			if e in ["tscn", "tres", "scn", "res"]:
				out.append(p)
		n = d.get_next()
	d.list_dir_end()
