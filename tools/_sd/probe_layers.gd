extends SceneTree

# 打印 mud 节点下每个 Sprite2D 的真实屏幕矩形，确认三层到底叠在哪。

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame

	var mud := node.get_node_or_null("mud")
	if mud == null:
		print("[Probe] 找不到 mud"); quit(); return

	print("[Probe] mud 世界位置 = ", mud.global_position, "  scale=", mud.scale)

	for c in mud.get_children():
		if c is Sprite2D:
			var t: Texture2D = c.texture
			var ts := Vector2.ZERO
			if t != null:
				ts = t.get_size()
			print("--- %s  z=%d" % [c.name, c.z_index])
			print("    texture=%s size=%s" % [t.resource_path if t else "NULL", ts])
			print("    position=%s scale=%s offset=%s" % [c.position, c.scale, c.offset])
			print("    region_enabled=%s region_rect=%s" % [c.region_enabled, c.region_rect])
			print("    material=%s" % c.material)
			# 有效尺寸（region 或整图）
			var eff := ts
			if c.region_enabled:
				eff = c.region_rect.size
			var w: float = eff.x * c.scale.x
			var h: float = eff.y * c.scale.y
			var cx: float = c.global_position.x + c.offset.x * c.scale.x
			var cy: float = c.global_position.y + c.offset.y * c.scale.y
			print("    屏幕矩形: x[%.1f, %.1f]  y[%.1f, %.1f]" % [cx - w * 0.5, cx + w * 0.5, cy - h * 0.5, cy + h * 0.5])

	# 摄像机可见范围
	var cam := node.get_viewport().get_camera_2d()
	if cam != null:
		print("[Probe] 摄像机世界中心 = ", cam.get_screen_center_position(), " zoom=", cam.zoom)

	quit()
