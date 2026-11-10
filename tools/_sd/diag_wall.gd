extends SceneTree

# 诊断：战场空气墙的视觉节点到底存不存在、透明度是多少、位置在哪
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(10):
		await process_frame

	var box: Node2D = scn.get_node("box")
	box.global_position = Vector2(6900.0, 684.0)
	var arena: Node = scn.get_node("SnakeBossArena")
	var boss: Node = arena.get_node("SnakeBoss")

	var t := 0.0
	while t < 25.0:
		await create_timer(0.2).timeout
		t += 0.2
		if boss.State == 2:
			break

	box.global_position = Vector2(6450.0, 684.0)
	await create_timer(0.6).timeout

	print("[Diag] arena children = %d" % arena.get_child_count())
	for c in arena.get_children():
		var vis := c.get_node_or_null("Visual")
		print("[Diag]   %s  type=%s  vis=%s" % [c.name, c.get_class(), "有" if vis else "无"])
		if vis != null:
			print("[Diag]     pos=%s  modulate=%s  tex=%s  size=%s" % [
				vis.global_position, vis.modulate,
				"null" if vis.texture == null else vis.texture.resource_path,
				vis.texture.get_size() if vis.texture != null else Vector2.ZERO])
			print("[Diag]     z=%d  zrel=%s  scale=%s" % [vis.z_index, vis.z_as_relative, vis.scale])
	var img := root.get_viewport().get_texture().get_image()
	if img != null:
		img.save_png("res://docs/screenshots/nw_diag.png")
		# 采样墙面所在竖条（世界 x=6404 → 屏幕 x≈53）
		for x in [20, 40, 53, 70, 90]:
			var out := []
			for y in [80, 160, 240, 320]:
				out.append(str(img.get_pixel(x, y)))
			print("[Diag] x=%d -> %s" % [x, " ".join(out)])
	quit()
