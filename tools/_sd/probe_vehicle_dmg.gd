extends SceneTree

# 核对六张图的载具子弹伤害是否都变成 25
func _initialize() -> void:
	for name in ["firstry", "underground", "rockhouseinside", "jungle", "jungleinside", "eden"]:
		var path := "res://%s.tscn" % name
		if not ResourceLoader.exists(path):
			print("[Veh] %s 不存在" % name)
			continue
		var scn: Node2D = load(path).instantiate()
		root.add_child(scn)
		for i in range(4):
			await process_frame
		var box: Node2D = scn.get_node_or_null("box")
		if box != null:
			print("[Veh] %-16s VehicleBulletDamage = %d" % [name, box.VehicleBulletDamage])
		scn.queue_free()
		await process_frame
	quit()
