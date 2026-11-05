extends SceneTree
func _initialize() -> void:
	var node: Node = load("res://jungle.tscn").instantiate()
	root.add_child(node)
	await process_frame
	await physics_frame
	var space := root.world_2d.direct_space_state
	var prev := -9999.0
	var run_start := 0
	var best := 0
	var best_x := 0
	for x in range(300, 5200, 25):
		var q := PhysicsRayQueryParameters2D.create(Vector2(x, -200), Vector2(x, 900))
		q.collision_mask = 1
		var hit := space.intersect_ray(q)
		var y: float = hit.position.y if not hit.is_empty() else -1.0
		if abs(y - prev) < 0.5:
			best += 1
			if best > 40:
				print("平地段 x=%d..%d  y=%.1f" % [run_start, x, y])
				best = 0
		else:
			run_start = x
			best = 0
		prev = y
	quit()
