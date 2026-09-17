# Blind（SetDataPointVisible）表达式填写说明

本文说明 Blind 批量文件中 `LogicText` 的推荐写法、程序支持的操作符、激活链建议补全和错误处理方式。

> **重要原则**：程序生成的 `SuggestedLogicText` 仅供人工审阅，不会覆盖原始 `LogicText`。真正生成 ECS 时使用的仍是用户上传的原始 `LogicText`。有错误时仍可强制生成，但结果必须由用户自行复核。

## 快速示例总表

下表列名与 SetDataPointVisible 页面一致。示例假设 SDS 中对应字段均位于指定 Form；实际结果以当前上传的 SDS 和本次上传的完整激活链为准。

| BlindOID | FolderOID | FormOID | FieldOID | LogicText |
| --- | --- | --- | --- | --- |
| B001 | V1 | DM | CHILDPOT | `SEX = 2` | `SEX = 2` |
| B002 | V1 | DM | CHILDREA | `SEX = 2 and CHILDPOT = 2` | `SEX = 2 and CHILDPOT = 2` |
| B003 | V1 | VS | TEMPMH | `TPCLSIG = 2 or TPCLSIG = 3` | `TPCLSIG = 2 or TPCLSIG = 3` |
| B004 | V1 | VS | TEMPMH | `TPCLSIG = 2 or 3` | `TPCLSIG = 2 or 3` |
| B005 | V1 | DM | CHILDREA | `SEX = 2 and CHILDPOT = 2, set CHILDREA visible` | `SEX = 2 and CHILDPOT = 2` |
| B006 | V1 | DM | CHILDREA | `SEX = 2 and CHILDPOT = 2，激活 CHILDREA` | `SEX = 2 and CHILDPOT = 2` |
| B007 | V1 | DM | CHILDREA | `SEX（same visit） = 2 and CHILDPOT = 2` | `SEX = 2 and CHILDPOT = 2` |
| B008 | V1 | VS | TEMPMH | `(TPCLSIG = 2 or 3) and VSPERF = 1` | `(TPCLSIG = 2 or 3) and VSPERF = 1` |
| B009 | V1 | DM | `CHILDPOT/CHILDREA` | `SEX = 2` | `SEX = 2` |
| B010 | V1 | DM | CHILDREA | `CHILDPOT = 2` | `SEX = 2 and CHILDPOT = 2` |


表中的“留空”表示该示例没有错误或无法产生可靠建议。B010 只有在同一 Folder/Form 的上传数据中同时存在“`SEX = 2` 激活 `CHILDPOT`”时才会补全。B017 用于提醒：校验通过不等于真实激活链完整。

## 1. 最正规的写法

Blind 的每一行表示：在指定 Folder/Form 范围内，条件成立时激活 `FieldOID` 列中的一个或多个目标字段。

推荐写法：

```text
SEX = 2 and CHILDPOT = 2 and CHILDREA = 99
(TPCLSIG = 2 or TPCLSIG = 3) and VSPERF = 1
```

填写时建议：

1. 使用 SDS 中的真实 OID。
2. LogicText 中只写 FieldOID，不写 Folder/Form 定位。
3. 条件字段必须位于该行指定的同一张 Form 中。
4. 比较符、`and`、`or` 前后留空格。
5. 分组使用英文括号。
6. 多个条件尽量完整书写，例如 `A = 2 or A = 3`。

目标字段填写在 `FieldOID` 列。多个目标字段可使用 `/`、逗号、中文逗号、顿号、空格或换行分隔。

## 2. 支持的操作符

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

请勿使用 `=>` 或 `=<`。`and`、`or` 前后必须有空格。

空值推荐写成：

```text
FIELD = null
FIELD <> null
```

Blind 还支持特殊条件：

```text
NOW is present
```

## 3. LogicText的解析原则

程序只读取LogicText中逗号、中文逗号之前的第一段：
举例：

```text
SEX = 2 and CHILDPOT = 2, set CHILDREA visible
```

程序仅提取：

```text
SEX = 2 and CHILDPOT = 2
```

真正被激活的字段以该行 `FieldOID` 列为准。

## 4. 简写的 or 条件

以下两种写法均支持：

```text
TPCLSIG = 2 or TPCLSIG = 3 or TPCLSIG = 4
TPCLSIG = 2 or 3 or 4
```

注：简单枚举值可以使用简写；复杂条件应完整写出字段和比较符。

## 5. 激活链的自动补全

假设同一 Folder、同一 Form 中上传了：

```text
A = 1  激活 B
B = 1  激活 C
C = 1  激活 D
```

程序会自动补全“激活 D”的完整条件：

```text
A = 1 and B = 1 and C = 1
```

补全规则：

- 只在相同 FolderOID、FormOID 分组内寻找依赖；
- 不同访视可以有不同的激活链；
- 独立链不会被串在一起；
- 循环依赖会报错；
- 同一范围内存在多条候选激活规则时会报错；
- 已有括号分组的表达式视为人工确认内容，程序会保留原样并停止递归补全。

## 6. 建议补全的局限

程序只能使用本次上传的规则，无法知道未上传的业务逻辑。

真实链条可能是：

```text
A 激活 B → B 激活 C → C 激活 D
```

如果只上传 `C 激活 D`，程序不可能推断出缺失的 A 和 B。

> **“校验通过”只表示当前上传内容在结构和 SDS 关系上可解释，不表示激活链完整，也不表示补全建议一定符合真实业务。**

另外，程序也可能错误理解原本正确但较复杂的人工表达式。因此补全结果始终只是建议，不能直接当作生产逻辑。

## 7. 程序会自动处理什么

- 中文括号转为英文括号；
- 删除 `(same visit)`、`(the same visit)`；
- 接受简单的 `FIELD = 2 or 3`；
- 校验括号、比较符、`and/or` 和条件完整性；
- 校验条件字段和目标字段是否属于指定 Form；
- 为可以确定的同访视、同表单激活链生成建议；
- 某些行错误时继续处理其他可解析行。

程序不会自动修改原始 `LogicText`。

## 8. 出现笔误时会怎样

以下情况会显示错误并将行标红：

- BlindOID 为空或重复；
- FolderOID、FormOID 不存在；
- 目标 FieldOID 不属于指定 Form；
- LogicText 中的字段不属于指定 Form；
- 字段或比较符无法识别；
- `and/or` 前后没有空格；
- `and/or` 两侧条件不完整；
- 括号不匹配；
- 出现循环依赖或无法唯一确定的激活规则。

程序不会模糊猜测或自动改正 OID。用户可以取消错误勾选或强制生成，但生成器仍使用原始 `LogicText`。

## 9. 推荐审阅流程

```text
上传 Blind 文件
  ↓
查看错误和 SuggestedLogicText
  ↓
导出 Output/EditCheck/BlindValidation.xlsx
  ↓
人工逐行确认或修改
  ↓
将接受的建议覆盖到原 LogicText
  ↓
重新上传并再次校验
  ↓
生成 ECS
```

即使校验失败也可强制生成，但后果由用户自行承担。
