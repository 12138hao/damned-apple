extends SceneTree

# 古井入场动画四连拍：井里 → 冒头 → 翻出井口 → 落地站好
# 固定相机 + 按 _t 抓帧（不按帧数，headless 帧率不稳）

const CROP := 300
const ZOOM := 1.7

var _cam: Camera2D
var _box: CharacterBody2D
var _well: Node2D
var _shots: Array[Image] = []

func _snap(tag: String) -> void:
	var full := root.get_viewport().get_texture().get_image()
	if full == null:
		print("!! 拿不到视口"); return
	var img := Vector2(full.get_width(), full.get_height())
	var screen := (_box.global_position - (_cam.global_position + _cam.offset)) * _cam.zoom + img * 0.5
	# 裁切框固定在井口附近，不跟着人跑（不然四张图景别不一致，看不出位移）
	var focus := (_well.global_position + Vector2(50, -40) - (_cam.global_position + _cam.offset)) * _cam.zoom + img * 0.5
	var x0 := clampi(int(focus.x - CROP * 0.5), 0, full.get_width() - CROP)
	var y0 := clampi(int(focus.y - CROP * 0.5), 0, full.get_height() - CROP)
	var piece := Image.create_empty(CROP, CROP, false, Image.FORMAT_RGBA8)
	piece.blit_rect(full, Rect2i(x0, y0, CROP, CROP), Vector2i.ZERO)
	piece.resize(int(CROP * ZOOM), int(CROP * ZOOM), Image.INTERPOLATE_NEAREST)
	_shots.append(piece)
	print("[Shot] ", tag, "  pos=", _box.global_position.snapped(Vector2(0.1, 0.1)), "  ", _well.call("DebugWell"))

func _initialize() -> void:
	root.get_node("GameState").call("MarkWellEntry")
	var node: Node = load("res://jungle.tscn").instantiate()
	root.add_child(node)
	await process_frame
	_box = node.get_node("box")
	_well = node.get_node("Well")

	_cam = Camera2D.new()
	_cam.zoom = Vector2(2.0, 2.0)
	_cam.position_smoothing_enabled = false
	node.add_child(_cam)
	_cam.global_position = Vector2(252, 318)
	_cam.make_current()

	# 等动画起来（_t > 0）
	while not str(_well.call("DebugWell")).contains("_t=0.0"):
		await process_frame
		if float(str(_well.call("DebugWell")).split("_t=")[1].split(" ")[0]) > 0.0:
			break

	var targets := [0.30, 0.75, 1.32, 2.10]
	for i in range(targets.size()):
		var tgt: float = targets[i]
		var guard := 0
		while true:
			var s := str(_well.call("DebugWell"))
			var t := float(s.split("_t=")[1].split(" ")[0])
			if t >= tgt or guard > 400:
				break
			await process_frame
			guard += 1
		await _snap("第%d张 t≈%.2f" % [i + 1, tgt])

	# 存一张全屏，看井在场景里的整体观感
	var full := root.get_viewport().get_texture().get_image()
	if full != null:
		full.save_png("res://docs/screenshots/_well_full.png")

	var w: int = int(CROP * ZOOM)
	var out := Image.create_empty(w * 2, w * 2, false, Image.FORMAT_RGBA8)
	for i in range(_shots.size()):
		out.blit_rect(_shots[i], Rect2i(0, 0, w, w), Vector2i((i % 2) * w, (i / 2) * w))
	out.save_png("res://docs/screenshots/well_entrance.png")
	print("[Shot] 完成 ", out.get_size())
	quit()
