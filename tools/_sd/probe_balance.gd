extends SceneTree

# 确认伤害数值只有一个来源：assets/weapons/*.tres + assets/balance/combat.tres
func _initialize() -> void:
	var cb = load("res://assets/balance/combat.tres")
	if cb == null:
		print("[Balance] combat.tres 加载失败")
	else:
		print("[Balance] combat.tres  FistDamage=%d" % cb.FistDamage)

	for name in ["bat", "ruler", "chalk"]:
		var w = load("res://assets/weapons/%s.tres" % name)
		if w == null:
			print("[Balance] %s.tres 加载失败" % name)
			continue
		print("[Balance] %-6s %s  DamageMin=%d DamageMax=%d Ranged=%s RangedDamage=%d" % [
			name, w.DisplayName, w.DamageMin, w.DamageMax, str(w.IsRanged), w.RangedDamage])

	# Box 上应该已经没有这些字段了（拿到 null 才是对的）
	var box_script = load("res://Box.cs")
	if box_script != null:
		var names = []
		for p in box_script.get_script_property_list():
			if str(p.name).find("Damage") >= 0:
				names.append(p.name)
		print("[Balance] Box 上仍带 Damage 字样的导出字段: %s" % str(names))
	quit()
