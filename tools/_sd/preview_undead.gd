extends SceneTree

# 生成「不死海盗苹果」的动画帧预览图（第二版：手绕手腕旋转）。
# 参数就是后面 C# 要复刻的那套。

const DIR := "res://assets/enemies/undead_apple/"
const CELL := 160
const COLS := 7
const ROWS := 4

var _body: Texture2D
var _hl: Texture2D
var _hr: Texture2D

const BODY_OFF := Vector2(0, -61.5)
const HAND_HALF := 20.0                       # 手中心相对手腕的 y 偏移
const WL0 := Vector2(-47.5, -29)              # 左手手腕静止位
const WR0 := Vector2(48, -29)                 # 右手手腕静止位

func _pose(bs: Vector2, br: float, bp: Vector2,
		wl: Vector2, wlr: float, wr: Vector2, wrr: float,
		aura: float = 0.0, smoke: float = 0.0) -> Dictionary:
	return {"bs": bs, "br": br, "bp": bp, "wl": wl, "wlr": wlr, "wr": wr, "wrr": wrr,
			"aura": aura, "smoke": smoke}

func _clips() -> Array:
	# 手：玩家在右侧为基准，镜像后自动适配左侧
	var walk := [
		_pose(Vector2(1.02, 0.98), -3.0, Vector2(0, 0),  WL0, 8.0,  WR0, -8.0),
		_pose(Vector2(0.99, 1.03), -1.0, Vector2(0, -3), WL0, 2.0,  WR0, -2.0),
		_pose(Vector2(1.02, 0.98), 3.0,  Vector2(0, -1), WL0, -8.0, WR0, 8.0),
		_pose(Vector2(0.99, 1.03), 1.0,  Vector2(0, -2), WL0, -2.0, WR0, 2.0),
	]
	var aim := [
		_pose(Vector2(1.0, 1.0), 3.0, Vector2(1, 0), Vector2(-46, -33), -18.0, Vector2(46, -33), -68.0),
		_pose(Vector2(1.0, 1.0), 4.0, Vector2(2, 0), Vector2(-45, -34), -20.0, Vector2(47, -34), -72.0),
	]
	var charge := [
		_pose(Vector2(1.02, 0.98), 2.0, Vector2(-1, 1), Vector2(-46, -34), -14.0, Vector2(42, -34), -50.0, 0.35),
		_pose(Vector2(1.04, 0.96), 3.0, Vector2(1, 0),  Vector2(-45, -35), -12.0, Vector2(40, -35), -46.0, 0.60),
		_pose(Vector2(1.06, 0.94), 4.0, Vector2(-1, 1), Vector2(-44, -36), -10.0, Vector2(38, -36), -42.0, 0.85),
		_pose(Vector2(1.08, 0.92), 5.0, Vector2(1, 2),  Vector2(-43, -37), -8.0,  Vector2(36, -37), -38.0, 1.00),
	]
	return [
		{"name": "呼吸 idle", "frames": [
			_pose(Vector2(1.0, 1.0), 0, Vector2.ZERO, WL0, 0, WR0, 0),
			_pose(Vector2(0.99, 1.03), 0, Vector2(0, -1), WL0 + Vector2(0, -2), 0, WR0 + Vector2(0, -2), 0),
			_pose(Vector2(1.0, 1.0), 0, Vector2.ZERO, WL0, 0, WR0, 0),
			_pose(Vector2(1.01, 0.97), 0, Vector2(0, 1), WL0 + Vector2(0, 1), 0, WR0 + Vector2(0, 1), 0),
		]},
		{"name": "走右 walk", "frames": walk},
		{"name": "走左 walk", "frames": walk, "flip": true},
		{"name": "发现 notice", "frames": [
			_pose(Vector2(1.16, 0.88), 0, Vector2(0, 2), WL0 + Vector2(-4, 4), 12.0, WR0 + Vector2(4, 4), -12.0),
			_pose(Vector2(0.92, 1.13), 0, Vector2(0, -6), WL0 + Vector2(-2, -11), -18.0, WR0 + Vector2(2, -11), 18.0),
			_pose(Vector2(1.03, 0.98), 0, Vector2(0, 0), WL0 + Vector2(0, -4), -6.0, WR0 + Vector2(0, -4), 6.0),
		]},
		{"name": "瞄准 aim", "frames": aim},
		{"name": "蓄力 charge", "frames": charge},
		{"name": "投技 throw", "frames": [
			_pose(Vector2(0.94, 1.06), 8.0, Vector2(3, -2),  Vector2(-38, -36), -30.0, Vector2(52, -36), -75.0, 0.3, 0.3),
			_pose(Vector2(0.92, 1.08), 10.0, Vector2(5, -1), Vector2(-30, -38), -45.0, Vector2(56, -38), -80.0, 0.2, 1.0),
			_pose(Vector2(1.0, 1.0), 2.0, Vector2(0, 0),     Vector2(-22, -36), -55.0, Vector2(50, -34), -80.0, 0.1, 0.8),
			_pose(Vector2(1.03, 0.97), -4.0, Vector2(-2, 0), Vector2(-44, -28), -10.0, Vector2(34, -26), -20.0, 0.0, 0.3),
		]},
	]

func _hand(root: Node2D, tex: Texture2D, wrist: Vector2, rot: float) -> void:
	var pivot := Node2D.new()
	pivot.position = wrist
	pivot.rotation_degrees = rot
	root.add_child(pivot)

	var s := Sprite2D.new()
	s.texture = tex
	s.position = Vector2(0, HAND_HALF)
	pivot.add_child(s)

func _make_cell(parent: Node2D, pose: Dictionary, at: Vector2, flip: bool) -> void:
	var root := Node2D.new()
	root.position = at
	if flip: root.scale = Vector2(-1, 1)
	parent.add_child(root)

	var aura: float = pose["aura"]
	if aura > 0.01:
		var c := Node2D.new()
		c.position = Vector2(0, -62)
		var a := aura
		c.draw.connect(func():
			var r := 34.0 + 16.0 * a
			c.draw_circle(Vector2.ZERO, r, Color(0.62, 0.25, 0.95, 0.16 * a))
			c.draw_circle(Vector2.ZERO, r * 0.62, Color(0.75, 0.45, 1.0, 0.20 * a))
		)
		root.add_child(c)

	var smoke: float = pose["smoke"]
	if smoke > 0.01:
		var s := Node2D.new()
		s.position = Vector2(6, -70)
		var sm := smoke
		s.draw.connect(func():
			var rng := RandomNumberGenerator.new()
			rng.seed = 20261105
			for i in range(10):
				var ang := rng.randf_range(-2.4, -0.6)
				var d := rng.randf_range(8.0, 40.0) * sm
				var p := Vector2(cos(ang), sin(ang)) * d
				s.draw_circle(p, rng.randf_range(3.0, 8.0), Color(0.70, 0.40, 0.95, 0.32 * sm))
		)
		root.add_child(s)

	var b := Sprite2D.new()
	b.texture = _body
	b.scale = pose["bs"]
	b.rotation_degrees = pose["br"]
	b.position = BODY_OFF + pose["bp"]
	root.add_child(b)

	_hand(root, _hl, pose["wl"], pose["wlr"])
	_hand(root, _hr, pose["wr"], pose["wrr"])

func _initialize() -> void:
	_body = load(DIR + "undead_apple_body.png") as Texture2D
	_hl = load(DIR + "undead_apple_hand_l.png") as Texture2D
	_hr = load(DIR + "undead_apple_hand_r.png") as Texture2D
	if _body == null or _hl == null or _hr == null:
		print("贴图没导入"); quit(); return

	var vp := SubViewport.new()
	vp.size = Vector2i(CELL * COLS, CELL * ROWS + 28)
	vp.transparent_bg = false
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(vp)

	var bg := ColorRect.new()
	bg.color = Color(0.16, 0.17, 0.19)
	bg.size = Vector2(CELL * COLS, CELL * ROWS + 28)
	vp.add_child(bg)

	var layer := Node2D.new()
	vp.add_child(layer)

	var clips := _clips()
	for ci in range(clips.size()):
		var label := Label.new()
		label.text = clips[ci]["name"]
		label.position = Vector2(ci * CELL + 4, 2)
		label.add_theme_color_override("font_color", Color(1, 1, 1))
		label.add_theme_font_size_override("font_size", 13)
		layer.add_child(label)

	for ci in range(clips.size()):
		var frames: Array = clips[ci]["frames"]
		var flip: bool = clips[ci].get("flip", false)
		for fi in range(frames.size()):
			_make_cell(layer, frames[fi], Vector2(ci * CELL + CELL * 0.5, 28 + fi * CELL + 148), flip)

	for i in range(6):
		await process_frame

	var img := vp.get_texture().get_image()
	img.save_png("res://docs/screenshots/undead_apple_frames.png")
	print("[Preview] 完成 ", img.get_size())
	quit()
