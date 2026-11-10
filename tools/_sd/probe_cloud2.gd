extends SceneTree

# 验证云不会跑出自己这段，且两端会淡出
func _initialize() -> void:
	var scn: Node2D = load("res://jungle.tscn").instantiate()
	root.add_child(scn)
	for i in range(6):
		await process_frame

	for layerName in ["CloudLayer", "CloudLayer2"]:
		var layer: Node2D = scn.get_node(layerName)
		var kids := layer.get_children()
		var minx := 1e9
		var maxx := -1e9
		for c in kids:
			minx = min(minx, c.position.x)
			maxx = max(maxx, c.position.x)
		print("[Cloud] %s Length=%.0f 共 %d 朵  x 范围 %.0f ~ %.0f   alpha 范围 %.2f~%.2f" % [
			layerName, layer.Length, kids.size(), minx, maxx,
			kids[0].modulate.a, kids[kids.size()-1].modulate.a])

		# 把小云推到左边界，看透明度
		var c0: Sprite2D = kids[0]
		c0.position = Vector2(3, c0.position.y)
		await create_timer(0.1).timeout
		print("[Cloud]   推到 x=3 后 alpha=%.3f" % c0.modulate.a)
		c0.position = Vector2(layer.Length * 0.5, c0.position.y)
		await create_timer(0.1).timeout
		print("[Cloud]   放到中间 alpha=%.3f" % c0.modulate.a)
	quit()
