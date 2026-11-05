extends SceneTree

# 不死海盗苹果的「手」特写：验证两件事
#   ① 飞出去的骷髅手和长在身上的手一样大
#   ② 手飞出去的时候，身上那只手消失；飞回来 / 进入投技后重新出现
#
# 做法：自己建一台固定相机怼在怪脸上（不用玩家的相机，它会跟着人跑），
# 三帧裁同一块区域，直接并排比大小。

const CROP := 560
const ZOOM := 1.2
const CAM_ZOOM := 2.2

var _shots: Array[Image] = []
var _cam: Camera2D
var _root: Node
var _box: CharacterBody2D
var _enemy: CharacterBody2D

func _find_hand() -> Node:
	for c in _root.get_children():
		var s = c.get_script()
		if s != null and str(s.resource_path).ends_with("SkeletonHand.cs"):
			return c
	return null

func _snap(tag: String, focus := Vector2.INF) -> void:
	# 相机位置要在「渲染之前」设好：先挪相机，再等几帧让新取景真正画出来，
	# 否则截到的是上一帧（甚至相机还没生效时的空景）。
	if is_instance_valid(_enemy):
		var target: Vector2 = focus if focus != Vector2.INF else _enemy.global_position + Vector2(0, -46)
		_cam.global_position = target
		_cam.reset_smoothing()
	for i in range(6):
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

	var hands := 0
	if is_instance_valid(_enemy):
		var l = _enemy.get_node_or_null("Visual/HandL")
		var r = _enemy.get_node_or_null("Visual/HandR")
		if l != null and l.visible: hands += 1
		if r != null and r.visible: hands += 1
	print("[Shot] ", tag, "  身上可见的手=", hands, "  场上有飞手=", _find_hand() != null,
		"  玩家HP=", _box.get("Hp") if is_instance_valid(_box) else -1)

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

	_box.global_position = _enemy.global_position + Vector2(-200, -30)
	_enemy.set("RandomPatrol", false)
	_enemy.set("Speed", 0.0)   # 别让它巡逻走开，否则几秒后就走出画面了

	# 自建固定相机（玩家的相机会跟着玩家跑，没法做同框对比）
	_cam = Camera2D.new()
	_cam.zoom = Vector2(CAM_ZOOM, CAM_ZOOM)
	_cam.position_smoothing_enabled = false
	node.add_child(_cam)
	_cam.make_current()

	# ① 蓄力：两只手都还在
	for f in range(90):
		await physics_frame
	await _snap("蓄力(双手)")

	# ② 手刚射出去：右手的贴图应该从身上消失
	var hand: Node = null
	for f in range(180):
		await physics_frame
		hand = _find_hand()
		if hand != null: break
	if hand == null: print("!! 没等到 SkeletonHand")
	for f in range(3):
		await physics_frame
	# 取景放在「怪」和「飞出去的手」中间，两个都能进画面，才好比大小
	var mid := _enemy.global_position + Vector2(0, -46)
	if hand != null and is_instance_valid(hand):
		mid = (mid + hand.global_position) * 0.5
	await _snap("发射(飞手)", mid)

	# ③ 投技：手回到身上，两只手抓着人
	if hand != null:
		for f in range(180):
			if _find_hand() == null: break
			await physics_frame
	for f in range(30):
		await physics_frame
	await _snap("投技(双手)")

	var w: int = int(CROP * ZOOM)
	var out := Image.create_empty(w * _shots.size(), w, false, Image.FORMAT_RGBA8)
	for i in range(_shots.size()):
		out.blit_rect(_shots[i], Rect2i(0, 0, w, w), Vector2i(i * w, 0))
	out.save_png("res://docs/screenshots/undead_hand_check.png")
	print("[Shot] 完成 ", out.get_size())
	quit()
