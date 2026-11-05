extends SceneTree

# 贴着怪站 10 秒，数一数挨了几下、掉了多少血。玩家每次掉血立刻回满，所以能一直数下去。
# 用法：--script res://tools/_sd/probe_invuln.gd -- --scene=jungle --enemy=pirate_apple1

func _initialize() -> void:
	var scene_path := "res://jungle.tscn"
	var enemy_name := "pirate_apple1"
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--scene="): scene_path = "res://" + a.substr(8) + ".tscn"
		if a.begins_with("--enemy="): enemy_name = a.substr(8)

	var node: Node = load(scene_path).instantiate()
	root.add_child(node)
	await process_frame
	await physics_frame

	var box: CharacterBody2D = node.get_node("box")
	var enemy: CharacterBody2D = node.get_node(enemy_name)

	for c in node.get_children():
		if c is CharacterBody2D and c != enemy and c != box:
			c.set("Damage", 0)
			c.process_mode = Node.PROCESS_MODE_DISABLED

	box.global_position = Vector2(enemy.global_position.x - 40, enemy.global_position.y - 80)
	for i in range(200):
		await physics_frame
		if box.is_on_floor():
			break
	for i in range(20):
		await physics_frame

	var max_hp: int = int(box.get("MaxHp"))
	var last: int = int(box.call("get", "Hp"))
	var hits := 0
	var total := 0
	var detail: Array = []

	for i in range(600):
		await physics_frame
		var hp: int = int(box.call("get", "Hp"))
		if hp < last:
			hits += 1
			total += last - hp
			detail.append(last - hp)
			box.call("HealFull")
			last = max_hp
		elif hp > last:
			last = hp

	print("%s / %s  10 秒挨 %d 下，共掉 %d 血" % [scene_path.get_file().get_basename(), enemy_name, hits, total])
	print("   伤害序列: ", detail)
	print("   该怪 Damage=%s LeaveDistance=%s  |  玩家 HurtCooldown=%s"
		% [enemy.get("Damage"), enemy.get("LeaveDistance"), box.get("HurtCooldown")])
	quit()
