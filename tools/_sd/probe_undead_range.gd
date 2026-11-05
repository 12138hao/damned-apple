extends SceneTree

func _initialize() -> void:
	var node: Node = load("res://jungle.tscn").instantiate()
	root.add_child(node)
	await process_frame
	await physics_frame
	for n in ["undead_apple1", "undead_apple2"]:
		var e = node.get_node_or_null(n)
		if e == null: continue
		print("%s  HandMaxRange=%s  HandFlySpeed=%s  DetectRange=%s"
			% [n, e.get("HandMaxRange"), e.get("HandFlySpeed"), e.get("DetectRange")])
	quit()
