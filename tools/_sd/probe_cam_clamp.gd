extends SceneTree

# 扫一遍主场景，看哪些场景在攀爬时会把 Clamp 的上下限撞反
func _initialize() -> void:
	var scenes := ["firstry", "underground", "jungle", "eden"]
	for name in scenes:
		var path := "res://%s.tscn" % name
		if not ResourceLoader.exists(path):
			continue
		var scn: Node2D = load(path).instantiate()
		root.add_child(scn)
		for i in range(6):
			await process_frame
		var box: Node2D = scn.get_node_or_null("box")
		if box == null:
			print("[Cam] %s 无 box" % name)
			scn.queue_free()
			continue
		var cam: Camera2D = box.get_node_or_null("Camera2D")
		var halfH: float = root.get_visible_rect().size.y * 0.5
		var baseC: float = box.CameraFixedY + cam.offset.y
		print("[Cam] %-12s Lock=%s FixedY=%.0f off=%.0f baseCenter=%.0f halfH=%.0f  %s" % [
			name, str(box.LockCameraY), box.CameraFixedY, cam.offset.y, baseC, halfH,
			"⚠️ 上下限反转" if baseC < halfH else "正常"])
		# 数一下可攀爬物
		var n := 0
		var stack := [scn]
		while stack.size() > 0:
			var cur = stack.pop_back()
			var s = cur.get_script()
			if s != null:
				var p := str(s.resource_path)
				if p.ends_with("Rope.cs") or p.ends_with("Vine.cs"):
					n += 1
			for c in cur.get_children():
				stack.append(c)
		print("[Cam]   %s 可攀爬物 %d 个" % [name, n])
		scn.queue_free()
		await process_frame
	quit()
