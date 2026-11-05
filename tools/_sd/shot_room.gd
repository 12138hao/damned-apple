extends SceneTree

# 只拍房间美术，把玩家和 NPC 都藏起来，方便看「画出来的地面」到底在哪
func _initialize() -> void:
	var node: Node = load("res://jungleinside.tscn").instantiate()
	root.add_child(node)
	current_scene = node
	await process_frame
	await physics_frame
	var box: CharacterBody2D = node.get_node("box")
	box.global_position = Vector2(400, -400)     # 挪出画面
	box.visible = false
	var npc = node.get_node_or_null("OldApple")
	if npc: npc.visible = false
	for i in range(20):
		await process_frame
	var img := root.get_viewport().get_texture().get_image()
	img.save_png("res://docs/screenshots/_room_plain.png")
	print("[Shot] ok  相机视口世界范围 x[%.0f..%.0f] y[%.0f..%.0f]"
		% [424 - 421, 424 + 421, 238 - 237, 238 + 237])
	quit()
