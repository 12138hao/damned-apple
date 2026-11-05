extends SceneTree

# 拍「狂按空格挣脱」提示（SpacePrompt）的样子：
#   ① 提示和玩家身体的相对大小、是不是斜上方
#   ② 键帽按下去 / 弹回来的两帧
#
# 固定相机 + 按玩家屏幕坐标裁切，别用玩家相机（它跟着人跑，裁切框追不上）。

const CROP := 200          # 裁切边长（世界像素）
const ZOOM := 3.0          # 拼图放大倍数
const CAM_ZOOM := 2.0

var _cam: Camera2D
var _root: Node
var _box: CharacterBody2D
var _enemy: CharacterBody2D
var _shots: Array[Image] = []
var _tag := ""

func _find_hand() -> Node:
	for c in _root.get_children():
		var s = c.get_script()
		if s != null and str(s.resource_path).ends_with("SkeletonHand.cs"):
			return c
	return null

func _snap(tag: String) -> void:
	# 相机不动，只要等两帧让 _Draw 跑完
	for i in range(3):
		await process_frame

	var full := root.get_viewport().get_texture().get_image()
	if full == null:
		print("!! 拿不到视口图像"); return

	if _shots.is_empty():
		full.save_png("res://docs/screenshots/_full_frame%s.png" % _tag)
		var pr := _box.get_node_or_null("SpacePrompt")
		if pr:
			print("   [探针] 提示 global_position=", pr.global_position,
				"  size=", pr.size, "  z=", pr.z_index,
				"  visible=", pr.visible, "  玩家屏幕=", (_box.global_position - (_cam.global_position + _cam.offset)) * _cam.zoom + Vector2(full.get_width(), full.get_height()) * 0.5,
				"  提示屏幕=", (pr.global_position - (_cam.global_position + _cam.offset)) * _cam.zoom + Vector2(full.get_width(), full.get_height()) * 0.5)

	# 世界 → 屏幕：screen = (world - (cam.pos + cam.offset)) * zoom + viewport/2
	var vp := Vector2(full.get_width(), full.get_height())
	var screen := (_box.global_position - (_cam.global_position + _cam.offset)) * _cam.zoom + vp * 0.5
	var x0 := int(screen.x - CROP * 0.5)
	var y0 := int(screen.y - CROP * 0.5)
	x0 = clampi(x0, 0, full.get_width() - CROP)
	y0 = clampi(y0, 0, full.get_height() - CROP)

	var piece := Image.create_empty(CROP, CROP, false, Image.FORMAT_RGBA8)
	piece.blit_rect(full, Rect2i(x0, y0, CROP, CROP), Vector2i.ZERO)
	piece.resize(int(CROP * ZOOM), int(CROP * ZOOM), Image.INTERPOLATE_NEAREST)
	_shots.append(piece)

	var prompt := _box.get_node_or_null("SpacePrompt")
	print("[Shot] ", tag,
		"  HP=", _box.get("Hp"),
		"  提示可见=", prompt.visible if prompt else "无节点",
		"  提示局部坐标=", prompt.position if prompt else "-",
		"  提示尺寸=", prompt.size if prompt else "-")

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

	# 站哪边：默认站怪的左边（提示会出现在玩家左上）。
	# 加 --right 参数就站到怪的右边，验证「提示左右翻转」那一支。
	var side := -1.0
	for a in OS.get_cmdline_user_args():
		if a == "right":
			side = 1.0
			_tag = "_right"
	_box.global_position = _enemy.global_position + Vector2(200 * side, -30)
	_enemy.set("RandomPatrol", false)
	_enemy.set("Speed", 0.0)

	# 固定相机：焦点放在「玩家和怪中间偏玩家」，这样提示不会被裁出画面
	_cam = Camera2D.new()
	_cam.zoom = Vector2(CAM_ZOOM, CAM_ZOOM)
	_cam.position_smoothing_enabled = false
	node.add_child(_cam)
	_cam.global_position = _enemy.global_position + Vector2(90 * side, -60)
	_cam.make_current()

	# 等骷髅手射出
	for f in range(600):
		await physics_frame
		if _find_hand() != null: break
	# 等手消失（抓住并拖回 → 投技开始）
	for f in range(300):
		if _find_hand() == null: break
		await physics_frame

	# 投技 1.25s ≈ 75 帧，连拍 3 张看键帽的按压节奏（周期 0.34s ≈ 20 帧）
	# 第 2、3 张之前各按两下空格，让进度条有内容（6 下才挣脱，按 4 下还不会脱手）
	for i in range(3):
		if i > 0:
			for k in range(2):
				Input.action_press("jump")
				await physics_frame
				Input.action_release("jump")
				await physics_frame
		await _snap("提示%d" % (i + 1))
		for f in range(6):
			await physics_frame

	var w: int = int(CROP * ZOOM)
	var out := Image.create_empty(w * 3, w, false, Image.FORMAT_RGBA8)
	for i in range(_shots.size()):
		out.blit_rect(_shots[i], Rect2i(0, 0, w, w), Vector2i(i * w, 0))
	out.save_png("res://docs/screenshots/space_prompt%s.png" % _tag)
	print("[Shot] 完成 ", out.get_size())
	quit()
