extends SceneTree

# 临时探针：打印指定 Sprite2D 的世界矩形（含 region 处理），用于核对前景裁剪。

func _rect_of(s: Sprite2D) -> Rect2:
	var tex: Texture2D = s.texture
	if tex == null:
		return Rect2()
	var size: Vector2
	if s.region_enabled:
		size = s.region_rect.size
	else:
		size = tex.get_size()
	var drawn: Vector2 = size * s.scale
	var center: Vector2 = s.global_position + s.offset * s.scale
	return Rect2(center - drawn * 0.5, drawn)

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame

	for p in ["mud/Foreground", "mud2/Foreground", "mud/Sprite2D", "mud2/Sprite2D",
			"road/Sprite2D", "road2/Sprite2D", "road3/Sprite2D"]:
		var s: Sprite2D = node.get_node_or_null(p)
		if s == null:
			print("%-18s 找不到" % p)
			continue
		var r := _rect_of(s)
		print("%-18s z=%-4d x[%.1f, %.1f] 宽 %.1f" % [p, s.z_index, r.position.x, r.end.x, r.size.x])

	quit()
