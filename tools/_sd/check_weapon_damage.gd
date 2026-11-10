extends SceneTree

# 端到端验证：武器伤害现在只认 assets/weapons/*.tres。
# 做法：把木棒的 .tres 临时改成 77，站到怪旁边挥一棒，看掉血是不是 77，然后还原。
func _initialize() -> void:
	var scn: Node = load("res://firstry.tscn").instantiate()
	root.add_child(scn)
	for i in range(6):
		await process_frame

	var box: Node2D = scn.get_node_or_null("box")
	if box == null:
		print("[Wpn] 找不到 box")
		quit()
		return

	var bat = load("res://assets/weapons/bat.tres")
	var keep := [bat.DamageMin, bat.DamageMax]
	bat.DamageMin = 77
	bat.DamageMax = 77
	print("[Wpn] 木棒 .tres 临时改成 %d~%d（原来 %d~%d）" % [
		bat.DamageMin, bat.DamageMax, keep[0], keep[1]])

	# 找一只带血量的普通怪当靶子
	var target: Node2D = null
	for n in scn.get_children():
		if n is CharacterBody2D and n.name != "box" and "Hp" in n:
			target = n
			break
	if target == null:
		print("[Wpn] 场景里没有敌人")
		bat.DamageMin = keep[0]
		bat.DamageMax = keep[1]
		quit()
		return

	box.global_position = target.global_position + Vector2(-26, 0)
	# GiveWeapon 从 GDScript 调不到（带默认参数的 C# 方法没被绑定），直接塞武器槽
	var slots = box.Weapons
	slots[0] = bat
	box.Weapons = slots
	await create_timer(0.3).timeout

	# 拿在手上（否则挥不出去）
	Input.action_press("slot1")
	await create_timer(0.2).timeout
	Input.action_release("slot1")
	await create_timer(0.2).timeout

	# 鼠标指到玩家右边，让攻击方向朝靶子
	var mm := InputEventMouseMotion.new()
	mm.position = Vector2(600, 225)
	Input.parse_input_event(mm)
	await create_timer(0.15).timeout

	var hp0: int = target.Hp
	Input.action_press("attack")
	await create_timer(0.75).timeout
	Input.action_release("attack")
	await create_timer(0.35).timeout
	print("[Wpn] 靶子 %s  HP %d -> %d（掉 %d）" % [target.name, hp0, target.Hp, hp0 - target.Hp])

	bat.DamageMin = keep[0]
	bat.DamageMax = keep[1]
	print("[Wpn] 已还原成 %d~%d" % [bat.DamageMin, bat.DamageMax])
	quit()
