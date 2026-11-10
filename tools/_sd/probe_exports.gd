extends SceneTree

# 把黑蛇身上所有可调数值列一遍，确认都成了 inspector 里的导出项
func _initialize() -> void:
	var arena: Node = load("res://SnakeBossArena.tscn").instantiate()
	root.add_child(arena)
	for i in range(4):
		await process_frame

	var boss: Node = arena.get_node("SnakeBoss")
	print("[Export] ---- 黑蛇导出字段 ----")
	for p in boss.get_property_list():
		var usage = p.usage
		if (usage & PROPERTY_USAGE_EDITOR) == 0:
			continue
		var nm: String = p.name
		if not (nm.begins_with("Enrage") or nm.begins_with("Idle") or nm.begins_with("Spit")
				or nm.begins_with("Pool") or nm.begins_with("Tail") or nm.begins_with("Apple")
				or nm.begins_with("Summon") or nm.begins_with("Mouth") or nm.begins_with("Body")
				or nm == "MaxHp" or nm == "DeadTime"):
			continue
		print("[Export] %-18s = %s" % [nm, str(boss.get(nm))])
	quit()
