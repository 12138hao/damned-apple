extends SceneTree

func _walk(n: Node, depth: int) -> void:
	var line := "  ".repeat(depth) + n.name + " [" + n.get_class() + "]"
	if n is Node2D:
		line += "  pos=" + str((n as Node2D).global_position.round())
	if n.get_script() != null:
		line += "  script=" + str(n.get_script().resource_path).get_file()
	print(line)
	for c in n.get_children():
		_walk(c, depth + 1)

func _initialize() -> void:
	var node: Node = load("res://jungle.tscn").instantiate()
	root.add_child(node)
	await process_frame
	await physics_frame
	for c in node.get_children():
		if c is Node2D:
			print("%-22s %-18s pos=%s" % [c.name, c.get_class(), (c as Node2D).global_position.round()])
	quit()
