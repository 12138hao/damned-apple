extends SceneTree

# 端到端验证传送门：加载场景 → 站到门前 → 按 E → 看是不是换了场景、落点对不对。
# 用法：-- --scene=jungle --x=7330 --y=345  /  --scene=jungleinside --x=170 --y=341

func _initialize() -> void:
	var scene_path := "res://jungle.tscn"
	var x := 7330.0
	var y := 345.0
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--scene="): scene_path = "res://" + a.substr(8) + ".tscn"
		if a.begins_with("--x="): x = float(a.substr(4))
		if a.begins_with("--y="): y = float(a.substr(4))

	var node: Node = load(scene_path).instantiate()
	root.add_child(node)
	current_scene = node
	await process_frame
	await physics_frame

	var box: CharacterBody2D = node.get_node("box")
	box.global_position = Vector2(x, y)
	box.velocity = Vector2.ZERO
	for i in range(40):
		await process_frame

	print("  ~ 场景里的 Area2D:")
	for c in node.get_children():
		if c is Area2D:
			var sp := "<无脚本>"
			if c.get_script() != null: sp = str(c.get_script().resource_path)
			print("    ", c.name, "  pos=", c.global_position.round(), "  script=", sp,
				"  R=", (c.get("InteractDistance") if sp.ends_with("Door.cs") else "-"))

	# 找到这扇门
	var door: Node2D = null
	for c in node.get_children():
		if c is Area2D and c.get_script() != null and str(c.get_script().resource_path).ends_with("Door.cs"):
			if c.global_position.distance_to(box.global_position) <= float(c.get("InteractDistance")):
				door = c
				break
	print("[前] 场景=%s  玩家=(%.0f, %.0f)" % [current_scene.name, box.global_position.x, box.global_position.y])
	if door == null:
		print("  !! 玩家不在任何门的范围内，按 E 也没用"); quit(); return
	print("  门: %s  pos=(%.0f, %.0f)  距离=%.0f  交互半径=%.0f"
		% [door.name, door.global_position.x, door.global_position.y,
		   door.global_position.distance_to(box.global_position), door.get("InteractDistance")])
	print("  目标: %s -> %s" % [door.get("TargetScene"), door.get("TargetSpawn")])

	Input.action_press("interact")
	for i in range(2):
		await process_frame
	Input.action_release("interact")
	for i in range(30):
		await process_frame

	var now := current_scene
	if now == null:
		print("  !! current_scene 为空"); quit(); return
	var box2 = now.get_node_or_null("box")
	print("[后] 场景=%s  玩家=%s" % [now.name, ("(%.0f, %.0f)" % [box2.global_position.x, box2.global_position.y]) if box2 else "找不到 box"])
	quit()
