extends SceneTree
func _initialize() -> void:
	var node: Node = load("res://jungleinside.tscn").instantiate()
	root.add_child(node); current_scene = node
	await process_frame; await physics_frame
	for i in range(10): await process_frame
	var npc: Node2D = node.get_node("OldApple")
	var spr: Sprite2D = npc.get_node("Sprite2D")
	var tex: Vector2 = spr.texture.get_size()
	var h: float = tex.y * spr.scale.y
	print("贴图=%s  scale=%s  世界高=%.1f" % [tex, spr.scale, h])
	print("精灵 global_position=%s  底部 y=%.1f" % [spr.global_position, spr.global_position.y + h * 0.5])
	print("NPC 原点 y=%.1f" % npc.global_position.y)
	quit()
