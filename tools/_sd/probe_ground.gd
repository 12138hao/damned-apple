extends SceneTree

# 把玩家直接放到指定坐标，看站不站得住、最后停在哪。
# 用法：-- --scene=jungle --x=7330 --y=400

func _initialize() -> void:
	var scene_path := "res://jungle.tscn"
	var x := 7330.0
	var y := 400.0
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--scene="): scene_path = "res://" + a.substr(8) + ".tscn"
		if a.begins_with("--x="): x = float(a.substr(4))
		if a.begins_with("--y="): y = float(a.substr(4))

	var node: Node = load(scene_path).instantiate()
	root.add_child(node)
	await process_frame
	await physics_frame

	var box: CharacterBody2D = node.get_node("box")
	box.global_position = Vector2(x, y)
	box.velocity = Vector2.ZERO
	for i in range(90):
		await physics_frame
	print("%s  (%.0f, %.0f)  ->  90 帧后 (%.1f, %.1f)  onFloor=%s"
		% [scene_path.get_file().get_basename(), x, y,
		   box.global_position.x, box.global_position.y, box.is_on_floor()])
	quit()
