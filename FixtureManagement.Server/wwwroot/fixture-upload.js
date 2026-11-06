function updateSelection() {
  const selectAll = document.getElementById('select-all');

  if (!selectAll) {
    return;
  }

  selectAll.onclick = () => {
    document.querySelectorAll('.add-fixture').forEach((fixture) => {
      fixture.checked = selectAll.checked;
    });
  };
}

export function onLoad() {
  updateSelection();
}

export function onUpdate() {
  updateSelection();
}