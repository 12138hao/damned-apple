extends SceneTree

# 排查：藤蔓的判定区到底有没有框、有没有和玩家重叠。

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame
	await physics_frame

	var box: CharacterBody2D = node.get_node("box")
	var v1: Node = node.get_node("Vine1")
	box.global_position = Vector2(620, 340)
	for i in range(12):
		await physics_frame

	var cs: CollisionShape2D = v1.get_node_or_null("CollisionShape2D")
	print("Area2D: monitoring=", v1.monitoring, " monitorable=", v1.monitorable,
		" layer=", v1.collision_layer, " mask=", v1.collision_mask,
		" script=", v1.get_script().resource_path if v1.get_script() else "无")
	print("判定框节点=", cs, " shape=", cs.shape if cs else "无",
		" size=", (cs.shape.size if cs and cs.shape else "-"),
		" 位置=", cs.position if cs else "-")
	print("玩家: layer=", box.collision_layer, " pos=", box.global_position)
	print("重叠的 body=", v1.get_overlapping_bodies())
	print("玩家形状=", box.get_node("CollisionShape2D").shape.size)
	print("WMarker=", v1.get_node_or_null("WMarker"), " 位置=", v1.get_node("WMarker").position)
	quit()
