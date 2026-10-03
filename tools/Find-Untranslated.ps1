param([Parameter(Mandatory)][string]$File)
$n = 0
Get-Content $File | ForEach-Object {
    $n++
    $l = $_
    if ($l -match '^\s*(using|//)' -or $l -match 'Loc\.(T|F)\(') { return }
    if ($l -match '"[^"]*[A-Za-zÄÖÜäöüß]{3,}[^"]*"') { "{0}:{1}: {2}" -f (Split-Path $File -Leaf), $n, $l.Trim() }
}
