# Пересобирает артефакт из живой страницы: та же вёрстка и стили,
# но описание опросника вшито внутрь, а кнопка PDF убрана —
# в артефакте нет сервера, который её обслуживает.
import io, re, json

WEB = r'C:\Users\Alex\Проекты Domeo\domeo-calculators\src\Domeo.Calculators.Web\wwwroot'
DST = 'form-preview.html'

live = io.open(WEB + r'\index.html', encoding='utf-8').read()
schema = json.load(io.open(WEB + r'\form.json', encoding='utf-8'))

style = re.search(r'<style>.*?</style>', live, re.S).group(0)
markup = re.search(r'<body>(.*?)<script>', live, re.S).group(1).strip()
script = re.search(r'<script>(.*?)</script>', live, re.S).group(1)

# Схема вместо сетевой загрузки.
script = script.replace('let schema = null;', 'const SCHEMA = __SCHEMA__;\nlet schema = null;')
script = re.sub(
    r'  try \{\n    const res = await fetch\("form\.json"\);.*?\n  \}\n',
    '  schema = SCHEMA;\n',
    script, flags=re.S)
script = script.replace('__SCHEMA__', json.dumps(schema, ensure_ascii=False))

# Кнопка PDF без сервера не работает — убираем её вместе с обработчиком.
markup = re.sub(r'\s*<button type="button" class="btn" id="btn-pdf">[^<]*</button>', '', markup)
script = re.sub(r'async function downloadPdf\(\) \{.*?\n\}\n\n', '', script, flags=re.S)
script = re.sub(r'[ ]*document\.getElementById\("btn-pdf"\)\.addEventListener[^\n]*\n', '', script)

markup = markup.replace(
    'Готовый лист можно скачать в PDF и передать дальше.',
    'Автономная копия для просмотра — рабочая версия отдаёт готовый PDF.')

out = ('<title>Передача проекта в комплектацию</title>\n'
       + style + '\n' + markup + '\n<script>\n' + script + '\n</script>\n')
io.open(DST, 'w', encoding='utf-8', newline='\n').write(out)
print('собрано:', len(out), 'символов')
