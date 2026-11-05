extends SceneTree

# 藤蔓验收：
#   ① 能爬上去，速度是「上慢」（UpSpeed=95 → 约 1.58 像素/帧）
#   ② 往下滑是加速的，最终到 SlideDownSpeed=430
#   ③ 爬的时候按 W/S 镜头 offset 不能动（老大明确要求「不触发向上向下看」）
#   ④ 从一根藤蹬开能抓到另一根
#
# GDScript 读不到 C# 的普通属性（IsClimbing / CurrentRope 都不是 [Export]），
# 所以全部靠「位置行为」判断：抓着藤时横向会被吸到藤的中线、竖向速度是 0。

const VINE1_X := 620.0
const VINE2_X := 760.0

var _box: CharacterBody2D
var _cam: Camera2D
var _v1: Node
var _v2: Node

func _log(tag: String) -> void:
	print("  %-16s y=%7.1f  x=%7.1f  vy=%7.1f  镜头offset=%s" % [
		tag, _box.global_position.y, _box.global_position.x,
		_box.velocity.y, str(_cam.offset)])

func _frames(n: int) -> void:
	for i in range(n):
		await physics_frame

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame

	_box = node.get_node_or_null("box")
	_v1 = node.get_node_or_null("Vine1")
	_v2 = node.get_node_or_null("Vine2")
	if _box == null or _v1 == null or _v2 == null:
		print("!! 节点缺失  box=%s vine1=%s vine2=%s" % [_box, _v1, _v2]); quit(); return
	_cam = _box.get_node_or_null("Camera2D")

	print("藤蔓1 长度=%.0f 顶端留白=%.0f  世界顶=%.0f 底=%.0f" % [
		_v1.get("Length"), _v1.get("TopPad"), _v1.global_position.y,
		_v1.global_position.y + float(_v1.get("Length"))])

	# 站到藤1底下
	_box.global_position = Vector2(VINE1_X, 340)
	await _frames(10)
	_log("站地上")

	# ── ① 按 W 往上爬 ──
	Input.action_press("look_up")
	print("--- 按住 W 往上爬（40 帧 ≈ 0.67 秒，应该爬 60 像素左右）---")
	var y0: float = _box.global_position.y
	for i in range(4):
		await _frames(10)
		_log("W 第%d0帧" % (i + 1))
	Input.action_release("look_up")
	var climb_speed: float = (y0 - _box.global_position.y) / (40.0 / 60.0)
	print("  → 实测上爬速度 %.1f 像素/秒（配置 95）" % climb_speed)

	# ── ③ 镜头在爬的时候不能动 ──
	Input.action_press("look_up")
	await _frames(20)
	var cam_up: float = _cam.offset.y
	Input.action_release("look_up")
	print("  → 爬的时候按住 W，镜头 offset.y = %.1f（必须是 0）" % cam_up)
	await _frames(2)
	# 对照组：先走开（站在藤上按 W 会立刻又抓住藤，镜头当然不动），再按 W
	_box.global_position = Vector2(400, 340)
	await _frames(6)
	Input.action_press("look_up")
	await _frames(20)
	var cam_look: float = _cam.offset.y
	Input.action_release("look_up")
	print("  → 离开藤蔓后按 W，镜头 offset.y = %.1f（应该明显小于 -80，说明上下看没被废掉）" % cam_look)
	await _frames(30)

	# ── 爬到顶端：应该停在 ClimbTopY，不被抛出去 ──
	_box.global_position = Vector2(VINE1_X, 340)
	await _frames(6)
	Input.action_press("look_up")
	for i in range(300):
		await physics_frame
		if _box.global_position.y <= 32.0: break
	await _frames(20)
	_log("爬到顶")
	print("  → 顶端 y 应该停在 %.0f 附近（藤蔓顶 -100 + 留白 130）" % (_v1.global_position.y + float(_v1.get("TopPad"))))
	Input.action_release("look_up")
	await _frames(10)
	_log("松开W")

	# ── ② 按 S 往下滑（加速）──
	print("--- 按住 S 往下滑（30 帧，速度应该越滑越快）---")
	Input.action_press("look_down")
	for i in range(3):
		await _frames(10)
		_log("S 第%d0帧" % (i + 1))
	Input.action_release("look_down")
	await _frames(20)

	# ── ④ 换藤：爬到高处，按跳 + 右 ──
	print("--- 换藤测试：先爬到 y≈250，再按跳+右 ---")
	Input.action_press("look_up")
	for i in range(200):
		await physics_frame
		if _box.global_position.y <= 250.0: break
	Input.action_release("look_up")
	await _frames(6)
	_log("起跳前")

	Input.action_press("move_right")
	Input.action_press("jump")
	await _frames(2)
	Input.action_release("jump")
	var grabbed := false
	for i in range(70):
		await physics_frame
		# 判定「抓到了藤2」：X 被吸到 760 附近，且在空中竖向速度为 0
		if abs(_box.global_position.x - VINE2_X) < 6.0 and abs(_box.velocity.y) < 1.0 and not _box.is_on_floor():
			grabbed = true
			_log("抓到藤2 第%d帧" % i)
			break
		if i % 10 == 0:
			_log("飞行中")
	Input.action_release("move_right")
	print("  → 换藤结果：", "成功" if grabbed else "失败")
	if grabbed:
		# 抓稳之后松开方向键，人应该挂在原地不动
		await _frames(20)
		_log("挂住藤2")
		await _frames(20)
		_log("再等20帧")

	quit()
