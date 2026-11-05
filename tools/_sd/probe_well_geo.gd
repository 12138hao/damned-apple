extends SceneTree

# 验证出井轨迹不穿模：只要「脚底 < 前沿顶边」时人还在井身 x 范围内，就是穿模

func _rect_of(spr: Sprite2D) -> Rect2:
	var r: Rect2 = spr.get_rect()
	var p0: Vector2 = spr.global_transform * r.position
	var p1: Vector2 = spr.global_transform * (r.position + r.size)
	return Rect2(p0, p1 - p0)

func _initialize() -> void:
	root.get_node("GameState").call("MarkWellEntry")
	var node: Node = load("res://jungle.tscn").instantiate()
	root.add_child(node)
	await process_frame

	var box: CharacterBody2D = node.get_node("box")
	var well: Node2D = node.get_node("Well")
	var back: Sprite2D = well.get_node("Back")
	var front: Sprite2D = well.get_node("Front")

	var rb := _rect_of(back)
	var rf := _rect_of(front)
	print("=== 对齐检查 ===")
	print("Back  屏幕矩形 = ", rb, "  底边=", rb.position.y + rb.size.y)
	print("Front 屏幕矩形 = ", rf, "  底边=", rf.position.y + rf.size.y)
	print("两者错位 = ", abs((rf.position.y + rf.size.y) - (rb.position.y + rb.size.y)), " 像素（应为 0）")

	# 井身 = 石砌部分（贴图 x 52..212，y 152..228）
	var stone_l: float = 162.0 + 52.0 * 0.6
	var stone_r: float = 162.0 + 212.0 * 0.6
	print("井身屏幕 x = [%.1f, %.1f]" % [stone_l, stone_r])

	var half: float = box.call("get", "HalfHeight")
	print("玩家 HalfHeight=", half)

	print("=== 轨迹（脚底 vs 前沿顶）===")
	var rim: float = rf.position.y
	var last := ""
	var bad := 0
	while true:
		await process_frame
		var s := str(well.call("DebugWell"))
		var cur: String = s.split("_t=")[1].split(" ")[0]
		if cur != last:
			last = cur
			var p: Vector2 = box.global_position
			var foot: float = p.y + half
			var inwell: bool = p.x + 16.0 > stone_l and p.x - 16.0 < stone_r
			var clip: bool = foot > rim + 0.5 and inwell
			if clip:
				bad += 1
			print("t=%s  pos=(%.1f, %.1f)  脚底=%.1f  在井身内=%s  %s" % [
				cur, p.x, p.y, foot, inwell, "★穿模" if clip else ""])
		if s.contains("running=False"):
			break
	print("穿模帧数 = ", bad)
	quit()
