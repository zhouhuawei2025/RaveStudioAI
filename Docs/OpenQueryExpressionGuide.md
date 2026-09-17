# OpenQuery 表达式填写说明

本文说明 OpenQuery 批量文件中 `LogicText` 的推荐写法、程序支持的操作符、省略规则和错误处理方式。

> **重要原则**：程序生成的 `NormalizedLogicText` 仅供审阅，不会覆盖原始 `LogicText`。调用 AI 时始终传入用户上传的原始 `LogicText`。有错误时仍可强制调用 AI，但结果必须由用户自行复核。

## 快速示例总表

下表列名与 OpenQuery 页面基本一致。业务示例取自真实项目素材并做了少量格式整理；实际规范结果仍以当前上传的 SDS 为准。

| QueryOID | FolderOID | FormOID | FieldOID | LogicText                                                          | NormalizedLogicText                                                                                                | 校验结果／程序处理 | Message | Error |
| --- | --- | --- | --- |--------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------| --- | --- | --- |
| SV003 | SCN | SV | VISDAT | `SCN.VISDAT - V2.VISDAT < -28d or > -2d`                           | `SCN.SV.VISDAT - V2.SV.VISDAT < -28d or > -2d`                                                                     | 两个 `Folder.Field` 均补全 Form；双边访视窗 | The V1 visit date exceeded the window. | 否 |
| SV004 | V1 | SV | VISDAT | `V1.VISDAT - V2.VISDAT <> -1d`                                     | `V1.SV.VISDAT - V2.SV.VISDAT <> -1d`                                                                               | 使用 `<>` 排除指定差值 | The V2(D-1) visit date exceeded the window. | 否 |
| SV008 | V5 | SV | VISDAT | `V5.VISDAT - V2.VISDAT < 5d or > 7d`                               | `V5.SV.VISDAT - V2.SV.VISDAT < 5d or > 7d`                                                                         | 访视窗下限或上限 | The V2(D7) visit date exceeded the window. | 否 |
| SV011 | V8 | SV | VISDAT | `V8.VISDAT - V2.VISDAT < 12d or > 16d and ENROLL.PART = 1`         | `V8.SV.VISDAT - V2.SV.VISDAT < 12d or > 16d and ALLVISIT.ENROLL.PART = 1`                                          | 日期区间附加跨表单条件 | The V2(D15) visit date exceeded the window. | 否 |
| SV012 | V8 | SV | VISDAT | `V8.VISDAT - V2.VISDAT <> 14d and ENROLL.PART = 2 or 3`            | `V8.SV.VISDAT - V2.SV.VISDAT <> 14d and ALLVISIT.ENROLL.PART = 2 or 3`                                             | 不等于、and 与枚举 or 简写组合 | The V2(D15) visit date exceeded the window. | 否 |
| CHEM010 | All visits | LB_CHEM | PERES | `LBRES = 2 and LBRESAE = null and LBRESMH = null`                  | `ALLVISIT.LB_CHEM.LBRES = 2 and ALLVISIT.LB_CHEM.LBRESAE = null and ALLVISIT.LB_CHEM.LBRESMH = null`               | 同表单多字段及空值组合 | If clinically significant is chosen, please supplement the related events. | 否 |
| CHEM011 | SCN | LB_CHEM | LBDAT | `SCN.LBDAT - V2.VISDAT < -28 or > -2`                              | `SCN.LB_CHEM.LBDAT - V2.SV.VISDAT < -28 or > -2`                                                                   | 不同表单、不同访视的数据点比较 | The Sample Collection Date exceeded the window. | 否 |
| EX048 | All visits | EX | EXSTDAT | `EXSTDAT <> VISDAT`                                                | `ALLVISIT.EX.EXSTDAT <> VISDAT`                                                                                    | `VISDAT` 有多个访视位置时无法唯一定位，应改为 `SV.VISDAT` 或明确访视 | The date of administration exceeded the window. | 是¹ |
| EX049 | All visits | EX | EXSTDAT1 | `EXSTDAT1 <> SV.VISDAT`                                            | `ALLVISIT.EX.EXSTDAT1 <> ALLVISIT.SV.VISDAT`                                                                       | 使用 Form.Field 表示不指定访视 | The date of administration exceeded the window. | 否 |
| EX053 | All visits | EX | EXENTIM2 | `EXENDAT2 + EXENTIM2 < EXSTDAT2 + EXSTTIM2`                        | `ALLVISIT.EX.EXENDAT2 + ALLVISIT.EX.EXENTIM2 < ALLVISIT.EX.EXSTDAT2 + ALLVISIT.EX.EXSTTIM2`                        | 日期和时间组合后直接比较 | End date/time cannot be earlier than start date/time. | 否 |
| EX054 | All visits | EX | EXENTIM2 | `(EXENDAT2 + EXENTIM2) - (EXSTDAT2 + EXSTTIM2) < 30min or > 90min` | `(ALLVISIT.EX.EXENDAT2 + ALLVISIT.EX.EXENTIM2) - (ALLVISIT.EX.EXSTDAT2 + ALLVISIT.EX.EXSTTIM2) < 30min or > 90min` | 日期时间差值及分钟区间 | The infusion duration is outside the expected range. | 否 |
| Q010 | V2 | SV | VISDAT | `VISDAT（same visit） > ICF.ICFDAT(in V1)`                           | `V2.SV.VISDAT > V1.ICF.ICFDAT`                                                                                     | 删除 same visit；转换中文括号和后置访视 | 请核对日期 | 否 |
| Q011 | V2 | SV | VISDAT | `V9.SV.VISDAT > VISDAT`                                            | `V9.SV.VISDAT > V2.SV.VISDAT`                                                                                      | Folder `V9` 不存在 | 请核对日期 | 是 |
| Q013 | V2 | SV | VISDAT | `PEDAT > VISDAT`                                                   | `PEDAT > V2.SV.VISDAT`                                                                                             | 裸字段 `PEDAT` 无法唯一定位 | 请核对日期 | 是 |
| Q014 | V2 | SV | VISDAT | `(VISDAT > V1.ICF.ICFDAT`                                          | `(V2.SV.VISDAT > V1.ICF.ICFDAT`                                                                                    | 表达式括号不匹配 | 请核对日期 | 是 |
| Q015 | V2 | SV | VISDAT | `VISDAT > 1 and 2`                                                 | `V2.SV.VISDAT > 1 and 2`                                                                                           | 逻辑连接词后只有常量，条件不完整 | 请核对访视日期 | 是 |

¹ 这一行专门展示真实素材中可能产生歧义的裸字段。若当前 SDS 中 `VISDAT` 恰好只能定位到一个 Folder/Form，则程序也可能直接补全并通过。`NormalizedLogicText` 只是程序建议，不能代替人工业务确认。

## 1. 最正规的写法

数据点推荐写成完整三段式：

```text
FolderOID.FormOID.FieldOID
```

例如：

```text
V2.SV.VISDAT - V1.ICF.ICFDAT > 3d
SCN.DM.AGE >= 18 and SCN.DM.SEX = 2
```

填写时建议：

1. 使用 SDS 中的真实 OID，不要使用显示名称或中文名称。
2. 比较符、`and`、`or` 前后留空格。
3. 逻辑分组使用英文括号。
4. 完整写出多个条件，例如 `A = 2 or A = 3`。

完整三段式会严格校验 Folder、Form、Field 是否存在，以及三者的从属关系。

## 2. 简写情况

以下规则中的“目标 Folder/Form/Field”，是指当前 Excel 行的 `FolderOID`、`FormOID` 和 `FieldOID`。程序会依据当前上传的 SDS 补全简写；无法唯一定位时会报错，不会猜测。

| 情况 | 允许的简写 | 程序补全方式 |
| --- | --- | --- |
| 字段在整个 EDC 中只有一个位置 | `FieldOID` | 使用 SDS 中唯一的 `FolderOID.FormOID.FieldOID` |
| 字段与目标数据点位于相同 Folder、相同 Form | `FieldOID` | 使用当前行的目标 Folder 和目标 Form |
| 字段与目标数据点位于相同 Folder、不同 Form | `FormOID.FieldOID` | 使用当前行的目标 Folder |
| 字段与目标数据点位于不同 Folder、相同 Form | `FolderOID.FieldOID` | 使用当前行的目标 Form |

### 1. 字段在整个 EDC 中只有一个位置

如果某个 FieldOID 在整个 EDC 中只能定位到唯一的 Folder 和 Form，可以只写 FieldOID。

例如：

```text
V2.SV.VISDAT - ICFDAT > 3d
AGE >= 18 and SEX = 2
```

上例成立的前提是 `ICFDAT`、`AGE` 和 `SEX` 分别都能从 SDS 唯一定位。若某字段出现在多个位置，程序会报告“裸字段无法唯一定位”。

### 2. 字段与目标数据点位于相同 Folder、相同 Form

如果条件字段和当前行目标字段位于同一个 Folder、同一个 Form，可以只写 FieldOID。

当前行：

```text
FolderOID = V2
FormOID   = SV
FieldOID  = VISDAT
```

可以写：

```text
VISDAT > 0
```

程序补全为：

```text
V2.SV.VISDAT > 0
```

即使 `VISDAT` 在其他访视中也存在，程序仍优先按当前行的目标 Folder 和目标 Form 理解。

### 3. 字段与目标数据点位于相同 Folder、不同 Form

Folder 与当前目标 Folder 相同，因此可以省略 Folder，只写：

```text
FormOID.FieldOID
```

例如当前目标 Folder 是 `SCN`：

```text
SV.VISDAT
```

程序补全为：

```text
SCN.SV.VISDAT
```

如果指定 Form 不属于当前目标 Folder，程序会将其规范为 `ALLVISIT.FormOID.FieldOID`，表示未指定具体访视。

### 4. 字段与目标数据点位于不同 Folder、相同 Form

Form 与当前目标 Form 相同，因此可以省略 Form，只写：

```text
FolderOID.FieldOID
```

例如当前目标 Form 是 `SV`：

```text
V1.VISDAT
```

程序补全为：

```text
V1.SV.VISDAT
```

如果该 Folder 中有多张表单包含同名 Field，程序会优先使用当前目标 Form；仍无法唯一确定时才会报错。

## 3. 支持的操作符

### 3.1 比较与逻辑操作符

| 含义 | 推荐写法 | 兼容写法 |
| --- | --- | --- |
| 等于 | `=` | — |
| 不等于 | `<>` | `!=`、`≠` |
| 大于 | `>` | `＞` |
| 小于 | `<` | `＜` |
| 大于等于 | `>=` | `≥`、`＞=` |
| 小于等于 | `<=` | `≤`、`＜=` |
| 并且 | `and` | `AND` 等大小写形式 |
| 或者 | `or` | `OR` 等大小写形式 |

请勿使用 `=>` 或 `=<`。建议在 `and`、`or` 前后保留空格。

### 3.2 空值判断

推荐写法：

```text
FIELD = null
FIELD <> null
```

程序还会自动转换：

```text
FIELD is empty      → FIELD = null
FIELD is not empty  → FIELD <> null
```

### 3.3 日期、时间与计算

支持常见的日期或时间组合、差值表达式，例如：

```text
V2.SV.VISDAT - V1.ICF.ICFDAT > 3d
EX.EXENDAT2 + EX.EXENTIM2 < EX.EXSTDAT2 + EX.EXSTTIM2
(EX.EXENDAT2 + EX.EXENTIM2) - (EX.EXSTDAT2 + EX.EXSTTIM2) < 30min or > 90min
```

常用时间单位：`d`（天）、`h`（小时）、`min`（分钟）、`Mon`（月）。复杂表达式主要由 AI 转换，基础校验通过不代表业务含义一定正确。

## 4. 访视后置写法

程序支持以下业务习惯：

```text
ICF.ICFDAT(in V1)  → V1.ICF.ICFDAT
VISDAT(in V2)      → V2.当前FormOID.VISDAT
```

中文括号会先转换为英文括号，因此 `VISDAT（in V2）` 也可以识别。

`(same visit)` 和 `(the same visit)` 会在校验前删除。

## 5. 简写的 or 条件

以下两种写法均允许：

```text
FIELD = 2 or FIELD = 3
FIELD = 2 or 3
```

程序不会把 `2 or 3` 自动改写，也不会因为该简写本身报错。复杂区间、日期时间组合或不同字段的条件应完整书写，避免歧义。

## 6. 程序会自动处理什么

- 中文括号转为英文括号；
- 中文逗号转为英文逗号；
- 长横线转为普通减号；
- 删除 `(same visit)`、`(the same visit)`；
- 规范空值判断；
- 清理点号两侧和多余空格；
- 按 SDS 补全能够唯一定位的数据点地址。

这些处理结果显示在 `NormalizedLogicText` 中，原始 `LogicText` 不会被覆盖。

## 7. 笔误校验

程序不会模糊猜测或自动改正 OID。以下情况会显示错误并将行标红：

- 必填列为空；
- FolderOID、FormOID 或 FieldOID 不存在；
- Field 不属于指定 Form；
- Form 不属于指定 Folder；
- 裸字段存在多个位置；
- `Folder.Field` 能匹配多张表单；
- 括号不匹配；
- `and` 后只有常量等明显不完整条件。

基础校验只能发现确定性错误，不能证明表达式的业务含义正确。

## 8. 推荐审阅流程

```text
上传 OpenQuery 文件
  ↓
查看校验错误和 NormalizedLogicText
  ↓
导出 Output/EditCheck/OpenQueryValidation.xlsx
  ↓
人工确认并修改原始 LogicText
  ↓
重新上传
  ↓
再次校验并调用 AI
```

即使校验失败也可强制调用 AI。此时传入的仍是原始 `LogicText`，后果由用户自行承担。
