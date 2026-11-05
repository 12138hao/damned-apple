extends SceneTree

func _initialize() -> void:
	root.get_node("GameState").call("MarkWellEntry")
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame
	var box: CharacterBody2D = node.get_node("box")
	var well: Node2D = node.get_node("Well")

	var t0 := Time.get_ticks_msec()
	for i in range(60):
		for k in range(4):
			await process_frame
		var ms := Time.get_ticks_msec() - t0
		print("  墙钟 %4d ms  pos=%s  %s" % [ms, str(box.global_position.snapped(Vector2(0.1,0.1))), well.call("DebugWell")])
		if ms > 2600: break
	quit()
