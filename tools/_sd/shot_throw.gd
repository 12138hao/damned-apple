extends SceneTree

# 投技「一滴一滴扣血 + 持续红闪 + 紫雾变浓」的验证。
#
# 两件事一起做：
#   ① 文字：投技期间逐帧打印 玩家HP / 贴图 Modulate（红闪就看这个）
#   ② 图片：投技期间连拍 4 帧拼图，看紫雾浓度和红闪

const CROP := 560
const ZOOM := 1.2
const CAM_ZOOM := 2.2
const SHOT_GAP := 2          # 两帧之间隔几个物理帧（红闪周期 0.09s ≈ 5.4 帧，间隔要小于它才抓得到红）

var _shots: Array[Image] = []
var _cam: Camera2D
var _root: Node
var _box: CharacterBody2D
var _enemy: CharacterBody2D
var _sprite: Sprite2D

func _find_hand() -> Node:
	for c in _root.get_children():
		var s = c.get_script()
		if s != null and str(s.resource_path).ends_with("SkeletonHand.cs"):
			return c
	return null

func _snap(tag: String) -> void:
	if is_instance_valid(_enemy):
		_cam.global_position = _enemy.global_position + Vector2(20, -50)
		_cam.reset_smoothing()
	for i in range(4):
		await process_frame

	var full := root.get_viewport().get_texture().get_image()
	if full == null:
		print("!! 拿不到视口图像"); return
	var img := Vector2(full.get_width(), full.get_height())
	var x0 := int((img.x - CROP) * 0.5)
	var y0 := int((img.y - CROP) * 0.5)

	var piece := Image.create_empty(CROP, CROP, false, Image.FORMAT_RGBA8)
	piece.blit_rect(full, Rect2i(x0, y0, CROP, CROP), Vector2i.ZERO)
	piece.resize(int(CROP * ZOOM), int(CROP * ZOOM), Image.INTERPOLATE_NEAREST)
	_shots.append(piece)
	print("[Shot] ", tag, "  HP=", _box.get("Hp"), "  Modulate=", _sprite.modulate,
		"  怪=", _enemy.global_position, " 玩家=", _box.global_position)

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	_root = node
	await process_frame

	_box = node.get_node_or_null("box")
	_enemy = node.get_node_or_null("undead_apple1")
	if _box == null or _enemy == null:
		print("!! 找不到节点"); quit(); return
	_sprite = _box.get_node_or_null("Sprite2D")

	_box.global_position = _enemy.global_position + Vector2(-200, -30)
	_enemy.set("RandomPatrol", false)
	_enemy.set("Speed", 0.0)   # 别让它巡逻走开，否则几秒后就走出画面了

	_cam = Camera2D.new()
	_cam.zoom = Vector2(CAM_ZOOM, CAM_ZOOM)
	_cam.position_smoothing_enabled = false
	node.add_child(_cam)
	_cam.make_current()

	# 等它把骷髅手射出来
	var hand: Node = null
	for f in range(600):
		await physics_frame
		hand = _find_hand()
		if hand != null: break
	if hand == null:
		print("!! 没等到骷髅手"); quit(); return

	# 等手抓到人并回到苹果面前（投技开始）
	for f in range(300):
		if _find_hand() == null: break
		await physics_frame

	print("--- 投技开始 ---")
	var lastHp: int = _box.get("Hp")
	for f in range(150):
		await physics_frame
		var hp: int = _box.get("Hp")
		if hp != lastHp:
			print("  f%-4d HP=%d  Modulate=%s" % [f, hp, str(_sprite.modulate)])
			lastHp = hp

	# 重新打一次，这次连拍
	_enemy.call("Respawn")
	await physics_frame
	_box.global_position = _enemy.global_position + Vector2(-200, -30)
	hand = null
	for f in range(600):
		await physics_frame
		hand = _find_hand()
		if hand != null: break
	for f in range(300):
		if _find_hand() == null: break
		await physics_frame

	# 投技 1.25 秒 ≈ 75 帧；扣血从 28% (0.35s≈21帧) 开始，
	# 所以等到第 26 帧再拍，正好是「血在一滴一滴掉 + 红闪」的中段
	for f in range(26):
		await physics_frame
	for i in range(4):
		await _snap("投技%d" % (i + 1))
		for f in range(SHOT_GAP):
			await physics_frame

	var w: int = int(CROP * ZOOM)
	# 拼成 2x2（横排 4 格太宽，看图时会被缩得看不清）
	var out := Image.create_empty(w * 2, w * 2, false, Image.FORMAT_RGBA8)
	for i in range(_shots.size()):
		out.blit_rect(_shots[i], Rect2i(0, 0, w, w), Vector2i((i % 2) * w, (i / 2) * w))
	out.save_png("res://docs/screenshots/undead_throw_ticks.png")
	print("[Shot] 完成 ", out.get_size())
	quit()
