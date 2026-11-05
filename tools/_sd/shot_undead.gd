extends SceneTree

# 抓三张关键帧（蓄力 / 骷髅手飞行 / 投技），围绕动作裁切放大后拼成一张。
#
# 两个要点：
# 1. 裁切必须把相机 offset 和 zoom 都算进去，否则框会整体偏出去。
# 2. 时间点不要按帧数硬猜（帧率一变就错位），改成盯场景里的 SkeletonHand 节点：
#    它一出现就说明手射出去了，消失就说明已经抓到/回收。

const W := 800
const H := 450
const CROP := 420          # 裁切边长（视口像素）
const ZOOM := 1.6

var _shots: Array[Image] = []
var _cam: Camera2D
var _root: Node
var _box: CharacterBody2D
var _enemy: CharacterBody2D

func _screen_of(world: Vector2, img_size: Vector2) -> Vector2:
	var scale := img_size / Vector2(W, H)      # 视口图像可能被窗口缩放（通常 1,1）
	var center := _cam.global_position + _cam.offset   # Camera2D 的取景中心
	return (world - center) * _cam.zoom * scale + img_size * 0.5

func _snap(tag: String, focusWorld: Vector2) -> void:
	for i in range(10):
		await process_frame

	var full := root.get_viewport().get_texture().get_image()
	if full == null:
		print("!! 拿不到视口图像（是不是用了 --headless？headless 没有渲染结果）")
		return
	var img_size := Vector2(full.get_width(), full.get_height())

	var p := _screen_of(focusWorld, img_size)
	var x0 := int(clampf(p.x - CROP * 0.5, 0, max(0.0, img_size.x - CROP)))
	var y0 := int(clampf(p.y - CROP * 0.5, 0, max(0.0, img_size.y - CROP)))

	var piece := Image.create_empty(CROP, CROP, false, Image.FORMAT_RGBA8)
	piece.blit_rect(full, Rect2i(x0, y0, CROP, CROP), Vector2i.ZERO)
	piece.resize(int(CROP * ZOOM), int(CROP * ZOOM), Image.INTERPOLATE_NEAREST)
	_shots.append(piece)
	print("[Shot] ", tag, " 视口 ", img_size, " 焦点屏幕 ", p, " 裁切于 ", x0, ",", y0)
	if _box != null and is_instance_valid(_box):
		print("        玩家 ", _box.global_position, " HP=", _box.get("Hp"),
			"  怪 ", _enemy.global_position if is_instance_valid(_enemy) else Vector2.ZERO)

func _find_hand() -> Node:
	# 不能靠名字找（重名节点会被自动改名），也不能靠 has_method（C# 方法不在 GDScript 的方法表里），
	# 认脚本资源路径最稳。
	for c in _root.get_children():
		var s = c.get_script()
		if s != null and str(s.resource_path).ends_with("SkeletonHand.cs"):
			return c
	return null

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	_root = node
	await process_frame

	var box: CharacterBody2D = node.get_node_or_null("box")
	var enemy: CharacterBody2D = node.get_node_or_null("undead_apple1")
	if box == null or enemy == null:
		print("!! 找不到节点"); quit(); return
	_box = box
	_enemy = enemy

	_cam = box.get_node_or_null("Camera2D")
	if _cam != null:
		_cam.zoom = Vector2(1.35, 1.35)
		_cam.position_smoothing_enabled = false

	box.global_position = enemy.global_position + Vector2(-200, -30)
	if _cam != null: _cam.reset_smoothing()

	# 关掉随机巡逻，让它站定不动 —— 否则它会一边走一边出画面，裁切框永远追不上
	enemy.set("RandomPatrol", false)
	enemy.set("Speed", 0.0)   # 别让它巡逻走开

	var body_focus := func() -> Vector2:
		return enemy.global_position + Vector2(0, -46)   # 怪的美术中心在原点上方

	var mid_focus := func() -> Vector2:
		var e := enemy.global_position + Vector2(0, -46)
		if box == null or not is_instance_valid(box): return e
		return (e + box.global_position + Vector2(0, -26)) * 0.5

	# ① 蓄力中段（发现0.45 + 瞄准0.3 + 蓄力1.0，第 90 帧约在蓄力 60%）
	for f in range(90):
		await physics_frame
	await _snap("charge", body_focus.call())

	# ② 等骷髅手出现，再走 6 帧让它飞离身体一段距离
	var hand: Node = null
	for f in range(180):
		await physics_frame
		hand = _find_hand()
		if hand != null: break
	if hand == null:
		print("!! 一直没看到 SkeletonHand")
	else:
		for f in range(6):
			await physics_frame
	await _snap("hand", mid_focus.call())

	# ③ 等骷髅手消失（抓到了 / 回收了），再等半个投技时长。
	#    投技总时长 1.0s，扣血在 35% 处，等 0.5s 正好是「举着人、紫烟最浓」那一段。
	if hand != null:
		for f in range(180):
			if _find_hand() == null: break
			await physics_frame
	for f in range(30):
		await physics_frame
	await _snap("throw", body_focus.call())

	# 拼图
	var w: int = int(CROP * ZOOM)
	var out := Image.create_empty(w * _shots.size(), w, false, Image.FORMAT_RGBA8)
	for i in range(_shots.size()):
		out.blit_rect(_shots[i], Rect2i(0, 0, w, w), Vector2i(i * w, 0))
	out.save_png("res://docs/screenshots/undead_apple_combat.png")
	print("[Shot] 完成 ", out.get_size())
	quit()
