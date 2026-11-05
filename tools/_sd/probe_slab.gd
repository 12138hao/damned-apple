extends SceneTree

# 平板地形改造后的几何 + 通行性验证。
#   ① 打印泥潭碰撞面的真实世界矩形、三个 Sprite 的矩形
#   ② 从左草地能不能直接走进泥潭（不被台阶卡住）
#   ③ 泥潭里单跳能不能上右侧草地；单跳不行的话二段跳行不行
#   ④ 泥里跳跃高度到底多少

func _shape_rect(sb: Node) -> String:
	for c in sb.get_children():
		if c is CollisionShape2D and c.shape is RectangleShape2D:
			var r: RectangleShape2D = c.shape
			var ctr: Vector2 = c.global_position
			var half: Vector2 = r.size * 0.5
			return "x[%.1f, %.1f] y[%.1f, %.1f]" % [
				ctr.x - half.x, ctr.x + half.x, ctr.y - half.y, ctr.y + half.y]
	return "无矩形碰撞"

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame

	var box: CharacterBody2D = node.get_node_or_null("box")
	var mud: Node = node.get_node_or_null("mud")
	var mz: Area2D = node.get_node_or_null("MudZone")

	print("=== 地形 ===")
	print("  road  碰撞 %s" % _shape_rect(node.get_node("road")))
	print("  mud   碰撞 %s" % _shape_rect(mud))
	print("  road2 碰撞 %s" % _shape_rect(node.get_node("road2")))

	for c in mud.get_children():
		if c is Sprite2D:
			var t: Texture2D = c.texture
			var eff: Vector2 = t.get_size()
			if c.region_enabled:
				eff = c.region_rect.size
			var w: float = eff.x * c.scale.x
			var h: float = eff.y * c.scale.y
			var cx: float = c.global_position.x + c.offset.x * c.scale.x
			var cy: float = c.global_position.y + c.offset.y * c.scale.y
			print("  %-11s z=%-4d x[%.1f, %.1f] y[%.1f, %.1f]" % [
				c.name, c.z_index, cx - w * 0.5, cx + w * 0.5, cy - h * 0.5, cy + h * 0.5])

	# ── ② 从左草地走进泥潭 ──
	box.global_position = Vector2(1180, 340)
	for i in range(80):
		await physics_frame
	print("\n=== 通行性 ===")
	print("  左草地站位 y=%.1f" % box.global_position.y)

	Input.action_press("move_right")
	for i in range(180):
		await physics_frame
	Input.action_release("move_right")
	for i in range(20):
		await physics_frame
	print("  往右走 180 帧后 x=%.1f y=%.1f 在泥里=%s  %s" % [
		box.global_position.x, box.global_position.y, box.get("InMud"),
		"走进去了" if box.global_position.x > 1300 else "被台阶卡住"])

	# ── ③ 走到右端，单跳能不能上去 ──
	box.global_position = Vector2(2760, 320)
	for i in range(80):
		await physics_frame
	var stand_y: float = box.global_position.y
	print("  泥潭右端站位 x=%.1f y=%.1f 在泥里=%s" % [
		box.global_position.x, box.global_position.y, box.get("InMud")])

	# 单跳
	Input.action_press("move_right")
	Input.action_press("jump")
	for i in range(6):
		await physics_frame
	Input.action_release("jump")
	for i in range(70):
		await physics_frame
		if box.global_position.x > 2830 and box.is_on_floor():
			break
	Input.action_release("move_right")
	for i in range(15):
		await physics_frame
	var single_ok: bool = box.global_position.x > 2830 and box.global_position.y < 350
	print("  单跳上右草地 -> x=%.1f y=%.1f 在地面=%s  %s" % [
		box.global_position.x, box.global_position.y, box.is_on_floor(),
		"成功" if single_ok else "没上去"])

	# 二段跳
	if not single_ok:
		box.global_position = Vector2(2760, 320)
		for i in range(60):
			await physics_frame
		Input.action_press("move_right")
		Input.action_press("jump")
		for i in range(5):
			await physics_frame
		Input.action_release("jump")
		for i in range(14):
			await physics_frame
		Input.action_press("jump")
		for i in range(5):
			await physics_frame
		Input.action_release("jump")
		for i in range(80):
			await physics_frame
			if box.global_position.x > 2830 and box.is_on_floor():
				break
		Input.action_release("move_right")
		for i in range(15):
			await physics_frame
		print("  二段跳上右草地 -> x=%.1f y=%.1f 在地面=%s  %s" % [
			box.global_position.x, box.global_position.y, box.is_on_floor(),
			"成功" if box.global_position.x > 2830 else "还是没上去"])

	# ── ④ 泥里跳跃高度 ──
	box.global_position = Vector2(2000, 300)
	for i in range(60):
		await physics_frame
	var y0: float = box.global_position.y
	Input.action_press("jump")
	var peak: float = y0
	for i in range(12):
		await physics_frame
		peak = min(peak, box.global_position.y)
	Input.action_release("jump")
	for i in range(70):
		await physics_frame
		peak = min(peak, box.global_position.y)
	print("\n  泥里单跳：站 y=%.1f 最高 y=%.1f 高度=%.1f 像素（满跳 66.7，比例 %.0f%%）" % [
		y0, peak, y0 - peak, (y0 - peak) / 66.7 * 100.0])

	quit()
