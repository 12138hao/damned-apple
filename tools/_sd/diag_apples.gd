extends SceneTree

# 数苹果：甩尾之后按 0.2s 采样场上 FallingApple 的数量，看是不是 3 波、每波约 7 个
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(10):
		await process_frame

	var box: Node2D = scn.get_node("box")
	box.global_position = Vector2(6900.0, 684.0)
	var arena: Node = scn.get_node("SnakeBossArena")
	var boss: Node = scn.get_node("SnakeBossArena/SnakeBoss")

	var t := 0.0
	while t < 40.0:
		await create_timer(0.1).timeout
		t += 0.1
		if boss.State == 4:
			break
	if boss.State != 4:
		print("[Apples] 没等到甩尾")
		quit()
		return

	# 先认一下苹果节点长什么样
	await create_timer(2.6).timeout
	for c in arena.get_children():
		var s = c.get_script()
		if s != null:
			var pp = c.position if c is Node2D else Vector2.ZERO
			print("[Apples] 子节点 %s script=%s pos=%s" % [c.name, s.resource_path, pp])

	var prev := -1
	for i in range(45):
		await create_timer(0.2).timeout
		var n := 0
		for c in arena.get_children():
			var s = c.get_script()
			if s != null and str(s.resource_path).ends_with("FallingApple.cs"):
				n += 1
		if n != prev:
			print("[Apples] t=%.1f  在场 %d" % [i * 0.2, n])
			prev = n
	quit()
