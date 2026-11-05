extends SceneTree

# 海盗苹果六连拍：站立 → 下砍蓄力 → 下砍挥击 → 横砍蓄力 → 横砍挥击 → 收招

const CROP := 400
const ZOOM := 1.25

var _cam: Camera2D
var _box: CharacterBody2D
var _enemy: CharacterBody2D
var _shots: Array = []

func _snap(tag: String) -> void:
	var full := root.get_viewport().get_texture().get_image()
	if full == null:
		print("!! 拿不到视口"); return
	var img := Vector2(full.get_width(), full.get_height())
	var focus := (_enemy.global_position + Vector2(-25, -70) - (_cam.global_position + _cam.offset)) * _cam.zoom + img * 0.5
	var x0 := clampi(int(focus.x - CROP * 0.5), 0, full.get_width() - CROP)
	var y0 := clampi(int(focus.y - CROP * 0.5), 0, full.get_height() - CROP)
	var piece := Image.create_empty(CROP, CROP, false, Image.FORMAT_RGBA8)
	piece.blit_rect(full, Rect2i(x0, y0, CROP, CROP), Vector2i.ZERO)
	piece.resize(int(CROP * ZOOM), int(CROP * ZOOM), Image.INTERPOLATE_NEAREST)
	_shots.append(piece)
	print("[Shot] ", tag, "  ", _enemy.call("DebugPirate"))

func _wait_state(st: String, tmin: float, guard_max: int = 900) -> bool:
	var g := 0
	while g < guard_max:
		var s := str(_enemy.call("DebugPirate"))
		if s.split("state=")[1].split(" ")[0] == st:
			var t := float(s.split("t=")[1].split(" ")[0])
			if t >= tmin:
				return true
		await process_frame
		g += 1
	return false

func _initialize() -> void:
	var node: Node = load("res://jungle.tscn").instantiate()
	root.add_child(node)
	await process_frame
	await physics_frame

	_box = node.get_node("box")
	_box.global_position = Vector2(3070, 100)

	# 先把怪按住（别让它发现玩家），否则等玩家落地这几秒它就自己开打了，
	# 六连拍会从半套连招的中间开始拍
	_enemy = node.get_node("pirate_apple1")
	_enemy.set("DetectRange", 0.0)
	_enemy.global_position = Vector2(3200, 360)

	for i in range(300):
		await physics_frame
		if _box.is_on_floor():
			break
	for i in range(10):
		await physics_frame

	_cam = Camera2D.new()
	_cam.zoom = Vector2(2.0, 2.0)
	_cam.position_smoothing_enabled = false
	node.add_child(_cam)
	_cam.global_position = Vector2(3130, 320)
	_cam.make_current()

	for i in range(40):
		await physics_frame

	await _snap("① 站立")
	_enemy.set("DetectRange", 460.0)
	await _wait_state("Windup", 0.18)
	await _snap("② 下砍·举刀")
	await _wait_state("Strike", 0.17)
	await _snap("③ 下砍·抡下")
	await _wait_state("Windup", 0.30)
	await _snap("④ 横砍·收刀")
	await _wait_state("Strike", 0.21)
	await _snap("⑤ 横砍·横扫")
	await _wait_state("Recover", 0.20)
	await _snap("⑥ 收招")

	var full := root.get_viewport().get_texture().get_image()
	if full != null:
		full.save_png("res://docs/screenshots/_pirate_full.png")

	var w: int = int(CROP * ZOOM)
	var out := Image.create_empty(w * 3, w * 2, false, Image.FORMAT_RGBA8)
	for i in range(_shots.size()):
		out.blit_rect(_shots[i], Rect2i(0, 0, w, w), Vector2i((i % 3) * w, (i / 3) * w))
	out.save_png("res://docs/screenshots/pirate_apple.png")
	print("[Shot] 完成 ", out.get_size())
	quit()
