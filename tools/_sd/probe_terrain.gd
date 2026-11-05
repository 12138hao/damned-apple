extends SceneTree

# 临时探针：列出 jungle.tscn 里所有 StaticBody2D / Area2D 的碰撞世界 AABB，
# 用来确定 x 3947~5991 这段区间的地形高度，好摆第二片泥浆。

func _aabb(node: Node) -> Rect2:
	var best := Rect2()
	var first := true
	for c in node.get_children():
		if c is CollisionShape2D and c.shape:
			var s: Shape2D = c.shape
			var ext: Vector2 = s.get_rect().size * 0.5
			var p: Vector2 = (c as CollisionShape2D).global_position
			var r := Rect2(p - ext, ext * 2)
			if first:
				best = r
				first = false
			else:
				best = best.merge(r)
		elif c is CollisionPolygon2D:
			var pts: PackedVector2Array = (c as CollisionPolygon2D).polygon
			var mn := Vector2(1e9, 1e9)
			var mx := Vector2(-1e9, -1e9)
			for v in pts:
				var w: Vector2 = (c as CollisionPolygon2D).global_position + v
				mn = mn.min(w)
				mx = mx.max(w)
			var r2 := Rect2(mn, mx - mn)
			if first:
				best = r2
				first = false
			else:
				best = best.merge(r2)
	return best

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame

	print("=== 场景里所有带碰撞的节点（世界坐标）===")
	for n in node.get_children():
		if n is StaticBody2D or n is Area2D:
			var r := _aabb(n)
			if r.size == Vector2.ZERO:
				continue
			print("%-14s x[%.1f, %.1f]  y[%.1f, %.1f]   宽 %.1f" % [
				n.name, r.position.x, r.end.x, r.position.y, r.end.y, r.size.x])

	print("")
	print("=== 玩家起点 ===")
	var box := node.get_node_or_null("box")
	if box:
		print("box 位置 ", box.global_position)

	quit()
