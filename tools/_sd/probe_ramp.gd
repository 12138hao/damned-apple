extends SceneTree

# 改了泥潭碰撞面高度（410 → 398）之后，重新验证两端斜坡还能不能进出。
# 泥里只能跳 20 像素，斜坡要是爬不上去，玩家进去就出不来了。

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame

	var box: CharacterBody2D = node.get_node_or_null("box")
	if box == null:
		print("[Probe] 找不到 box"); quit(); return

	# ① 从左地面走进泥潭
	box.global_position = Vector2(1180, 340)
	for i in range(90):
		await physics_frame
	print("[探针] 左地面站位 x=%.1f y=%.1f 在地面=%s" % [box.global_position.x, box.global_position.y, box.is_on_floor()])

	Input.action_press("move_right")
	var entered := false
	for i in range(200):
		await physics_frame
		if box.global_position.x > 1400:
			entered = true
			break
	Input.action_release("move_right")
	for i in range(20):
		await physics_frame
	print("[探针] 向左坡走进泥潭 -> x=%.1f y=%.1f 在泥里=%s 到达=%s" % [
		box.global_position.x, box.global_position.y, box.get("InMud"), entered])

	# ② 一直往右走，爬右坡出去
	Input.action_press("move_right")
	var out_x := 0.0
	for i in range(900):
		await physics_frame
		if box.global_position.x > 2840 and box.is_on_floor():
			out_x = box.global_position.x
			break
	Input.action_release("move_right")
	for i in range(20):
		await physics_frame
	print("[探针] 爬右坡 -> x=%.1f y=%.1f 在地面=%s 在泥里=%s 成功=%s" % [
		box.global_position.x, box.global_position.y, box.is_on_floor(), box.get("InMud"), out_x > 0])

	# ③ 泥里跳跃高度
	box.global_position = Vector2(2000, 330)
	for i in range(60):
		await physics_frame
	var y0: float = box.global_position.y
	Input.action_press("jump")
	var peak := y0
	for i in range(10):
		await physics_frame
		peak = min(peak, box.global_position.y)
	Input.action_release("jump")
	for i in range(80):
		await physics_frame
		peak = min(peak, box.global_position.y)
	print("[探针] 泥里起跳：站 y=%.1f 最高 y=%.1f 跳起高度=%.1f 像素" % [y0, peak, y0 - peak])

	quit()
