extends SceneTree

func _initialize() -> void:
	var u: Node = load("res://underground.tscn").instantiate()
	root.add_child(u)
	await process_frame
	var p: Node = u.get_node_or_null("RopePortal")
	if p == null:
		print("!! 找不到 RopePortal"); quit(); return
	print("RopePortal: TargetScene=", p.get("TargetScene"), "  WellEntry=", p.get("WellEntry"),
		"  TargetSpawn=", p.get("TargetSpawn"))
	print("位置=", p.global_position)
	quit()
