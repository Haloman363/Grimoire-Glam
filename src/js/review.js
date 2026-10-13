// Review page: builds an email to the shop. Nothing is stored or sent from the site itself.
(function () {
  'use strict';
  var byId = function (id) { return document.getElementById(id); };
  var form = byId('r-form');
  form.addEventListener('submit', function (e) {
    e.preventDefault();
    var err = byId('r-err');
    var picked = form.querySelector('input[name="rating"]:checked');
    var text = byId('r-text').value.trim();
    if (!picked || !text) {
      err.textContent = 'Please choose a star rating and write a few words.';
      err.classList.add('show');
      return;
    }
    err.classList.remove('show');
    var name = byId('r-name').value.trim() || 'A customer';
    var body = 'Rating: ' + picked.value + ' out of 5\n' +
      'Name: ' + name + '\n' +
      'OK to share on the website: ' + (byId('r-share').checked ? 'Yes' : 'No') + '\n\n' + text + '\n';
    var to = document.body.getAttribute('data-email') || '';
    window.location.href = 'mailto:' + to + '?subject=' + encodeURIComponent('New review: ' + picked.value + ' stars') + '&body=' + encodeURIComponent(body);
  });
})();
