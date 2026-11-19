// Karya Jaya 1978 - Storefront Vanilla JS (Laci Kasir Drawer & Rubber Stamp Toasts)
(function () {
  const formatRp = (num) => {
    const val = Math.round(Number(num) || 0);
    return 'Rp ' + val.toLocaleString('id-ID');
  };

  // Vintage Rubber-Stamp Toast Notification System (replaces browser alert())
  window.showStampToast = function (message, type = 'success') {
    const container = document.getElementById('vintage-toast-container');
    if (!container) return;

    const palette = {
      success: { bg: '#EAE0D0', border: '#2E4A3E', stamp: 'CAP TERIMA', color: '#2E4A3E' },
      error: { bg: '#EAE0D0', border: '#C85A32', stamp: 'PERHATIAN', color: '#C85A32' },
      info: { bg: '#EAE0D0', border: '#4A2E1B', stamp: 'CATATAN', color: '#4A2E1B' }
    }[type] || { bg: '#EAE0D0', border: '#231F20', stamp: 'INFO', color: '#231F20' };

    const toast = document.createElement('div');
    toast.className = 'stamp-toast pointer-events-auto max-w-sm w-full border-2 p-3.5 flex items-start gap-3';
    toast.style.backgroundColor = palette.bg;
    toast.style.borderColor = '#231F20';
    toast.style.boxShadow = '4px 4px 0px #231F20';

    const badge = document.createElement('span');
    badge.className = 'font-typewriter text-[10px] font-bold px-2 py-0.5 border-2 shrink-0 tracking-wider';
    badge.style.borderColor = palette.border;
    badge.style.color = palette.color;
    badge.style.transform = 'rotate(-4deg)';
    badge.textContent = palette.stamp;

    const text = document.createElement('p');
    text.className = 'text-sm font-semibold leading-snug text-[#231F20] flex-1';
    text.textContent = message;

    const closeBtn = document.createElement('button');
    closeBtn.type = 'button';
    closeBtn.className = 'font-typewriter text-xs font-bold text-[#231F20] hover:text-[#C85A32] px-1';
    closeBtn.textContent = '[X]';
    closeBtn.addEventListener('click', () => toast.remove());

    toast.append(badge, text, closeBtn);
    container.appendChild(toast);

    setTimeout(() => {
      if (toast.parentNode) toast.remove();
    }, 4500);
  };

  // Laci Kasir (Slide-Out Cash Register Cart Drawer)
  const drawer = document.getElementById('laci-kasir-drawer');
  const backdrop = document.getElementById('laci-kasir-backdrop');
  const itemsWrap = document.getElementById('laci-items-wrap');
  const summaryWrap = document.getElementById('laci-summary-wrap');
  const navCountBadge = document.getElementById('nav-cart-count');

  window.openCartDrawer = function () {
    if (!drawer || !backdrop) return;
    drawer.classList.remove('translate-x-full');
    backdrop.classList.remove('hidden');
    document.body.classList.add('overflow-hidden');
    window.refreshCartDrawer();
  };

  window.closeCartDrawer = function () {
    if (!drawer || !backdrop) return;
    drawer.classList.add('translate-x-full');
    backdrop.classList.add('hidden');
    document.body.classList.remove('overflow-hidden');
  };

  function renderDrawerState(data) {
    if (navCountBadge) {
      navCountBadge.textContent = String(data.totalPairs || 0);
    }

    if (!itemsWrap || !summaryWrap) return;

    const items = data.items || [];
    if (items.length === 0) {
      itemsWrap.innerHTML = `
        <div class="ticket-stub p-6 text-center my-6">
          <div class="font-typewriter text-xs uppercase tracking-widest text-[#4A2E1B] mb-2">LACI KASIR KOSONG</div>
          <p class="text-sm text-[#231F20]/80 mb-4">Belum ada pasang sepatu, sandal, atau boot yang dicatat.</p>
          <a href="/Products" class="btn-press btn-terracotta px-4 py-2 text-xs uppercase">Buka Katalog Pabrik</a>
        </div>
      `;
      summaryWrap.innerHTML = '';
      return;
    }

    let tierBannerHtml = '';
    if (data.tierProgress && data.tierProgress.bannerMessage) {
      const pct = Math.min(100, Math.max(0, data.tierProgress.progressPercent || 0));
      tierBannerHtml = `
        <div class="border-2 border-[#231F20] bg-[#EAE0D0] p-3 mb-4">
          <div class="flex items-center justify-between font-typewriter text-[11px] font-bold uppercase mb-1">
            <span>Status Harga Lusinan</span>
            <span>${data.totalPairs} Pasang</span>
          </div>
          <div class="w-full h-3 bg-[#F4EFE6] border border-[#231F20] overflow-hidden mb-1.5">
            <div class="h-full bg-[#2E4A3E] transition-all duration-300" style="width:${pct}%"></div>
          </div>
          <p class="text-xs font-semibold text-[#4A2E1B]">${escapeHtml(data.tierProgress.bannerMessage)}</p>
        </div>
      `;
    }

    const rowsHtml = items.map(item => `
      <div class="border-2 border-[#231F20] bg-[#F4EFE6] p-3 mb-3 flex flex-col gap-2">
        <div class="flex items-start justify-between gap-2">
          <div>
            <div class="font-typewriter text-[10px] text-[#4A2E1B] uppercase">${escapeHtml(item.sku)} • UKURAN ${item.size}</div>
            <a href="/Products/Details/${item.productId}" class="font-display font-bold text-sm text-[#231F20] hover:text-[#C85A32]">
              ${escapeHtml(item.productName)}
            </a>
            <div class="text-xs text-[#231F20]/75 flex items-center gap-1.5 mt-0.5">
              <span class="inline-block w-2.5 h-2.5 border border-[#231F20]" style="background:${escapeHtml(item.colorHex)}"></span>
              <span>${escapeHtml(item.colorName)}</span>
            </div>
          </div>
          <button type="button" data-cart-remove="${item.variantId}" class="font-typewriter text-xs font-bold text-[#C85A32] hover:underline">
            [HAPUS]
          </button>
        </div>
        <div class="flex items-center justify-between pt-2 border-t border-dashed border-[#231F20]/40">
          <div class="flex items-center border-2 border-[#231F20] bg-[#EAE0D0]">
            <button type="button" data-cart-dec="${item.variantId}" data-qty="${item.quantity}" class="px-2.5 py-0.5 font-typewriter font-bold hover:bg-[#4A2E1B] hover:text-[#F4EFE6]">-</button>
            <span class="px-3 py-0.5 font-typewriter font-bold text-sm bg-[#F4EFE6] border-x border-[#231F20]">${item.quantity}</span>
            <button type="button" data-cart-inc="${item.variantId}" data-qty="${item.quantity}" data-max="${item.availableStock}" class="px-2.5 py-0.5 font-typewriter font-bold hover:bg-[#4A2E1B] hover:text-[#F4EFE6]">+</button>
          </div>
          <div class="text-right">
            ${item.unitAppliedPrice < item.unitRetailPrice
              ? `<div class="text-[11px] line-through text-[#231F20]/50 font-typewriter">${formatRp(item.lineRetailTotal)}</div>`
              : ''}
            <div class="font-typewriter font-bold text-sm text-[#231F20]">${formatRp(item.lineAppliedTotal)}</div>
          </div>
        </div>
      </div>
    `).join('');

    itemsWrap.innerHTML = tierBannerHtml + rowsHtml;

    summaryWrap.innerHTML = `
      <div class="space-y-1.5 text-sm mb-4">
        <div class="flex justify-between">
          <span>Total Pasang</span>
          <span class="font-typewriter font-bold">${data.totalPairs} Pasang</span>
        </div>
        <div class="flex justify-between">
          <span>Subtotal Eceran</span>
          <span class="font-typewriter">${formatRp(data.grossRetailSubtotal)}</span>
        </div>
        ${data.discountAmount > 0 ? `
          <div class="flex justify-between text-[#2E4A3E] font-bold">
            <span>Potongan Lusinan (${data.discountPercent}%)</span>
            <span class="font-typewriter">- ${formatRp(data.discountAmount)}</span>
          </div>
        ` : ''}
        <div class="flex justify-between text-base font-bold pt-2 border-t-2 border-[#231F20]">
          <span>Total Netto</span>
          <span class="font-typewriter text-[#C85A32]">${formatRp(data.netSubtotal)}</span>
        </div>
      </div>
      <div class="grid grid-cols-2 gap-2">
        <a href="/Cart" class="btn-press btn-paper py-2.5 text-xs uppercase text-center">Lihat Keranjang</a>
        <a href="/Checkout" class="btn-press btn-leather py-2.5 text-xs uppercase text-center">Buat Faktur</a>
      </div>
    `;

    // Bind dynamic buttons
    itemsWrap.querySelectorAll('[data-cart-dec]').forEach(btn => {
      btn.addEventListener('click', () => {
        const vid = Number(btn.getAttribute('data-cart-dec'));
        const qty = Number(btn.getAttribute('data-qty'));
        if (qty <= 1) {
          removeCartItem(vid);
        } else {
          updateCartItem(vid, qty - 1);
        }
      });
    });

    itemsWrap.querySelectorAll('[data-cart-inc]').forEach(btn => {
      btn.addEventListener('click', () => {
        const vid = Number(btn.getAttribute('data-cart-inc'));
        const qty = Number(btn.getAttribute('data-qty'));
        const max = Number(btn.getAttribute('data-max'));
        if (qty >= max) {
          window.showStampToast(`Stok gudang hanya tersisa ${max} pasang.`, 'error');
          return;
        }
        updateCartItem(vid, qty + 1);
      });
    });

    itemsWrap.querySelectorAll('[data-cart-remove]').forEach(btn => {
      btn.addEventListener('click', () => {
        const vid = Number(btn.getAttribute('data-cart-remove'));
        removeCartItem(vid);
      });
    });
  }

  function escapeHtml(str) {
    if (str == null) return '';
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  window.refreshCartDrawer = async function () {
    try {
      const res = await fetch('/api/cart');
      if (!res.ok) return;
      const data = await res.json();
      renderDrawerState(data);
    } catch (e) {
      console.error(e);
    }
  };

  window.addToCartAjax = async function (variantId, quantity) {
    try {
      const res = await fetch('/api/cart/add', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ variantId: Number(variantId), quantity: Number(quantity) || 1 })
      });
      const data = await res.json();
      if (!res.ok || !data.success) {
        window.showStampToast(data.message || 'Gagal menambah ke Laci Kasir.', 'error');
        return;
      }
      renderDrawerState(data.summary);
      window.showStampToast(data.message, 'success');
      window.openCartDrawer();
    } catch (e) {
      window.showStampToast('Terjadi kesalahan jaringan saat menghubungi Laci Kasir.', 'error');
    }
  };

  window.addBundleToCartAjax = async function (bundleId) {
    try {
      const res = await fetch('/api/cart/add-bundle', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ bundleId: Number(bundleId) })
      });
      const data = await res.json();
      if (!res.ok || !data.success) {
        window.showStampToast(data.message || 'Gagal menambah Paket Usaha ke Laci Kasir.', 'error');
        return;
      }
      renderDrawerState(data.summary);
      window.showStampToast(data.message, 'success');
      window.openCartDrawer();
    } catch (e) {
      window.showStampToast('Terjadi kesalahan jaringan saat menghubungi Laci Kasir.', 'error');
    }
  };

  async function updateCartItem(variantId, quantity) {
    try {
      const res = await fetch('/api/cart/update', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ variantId, quantity })
      });
      const data = await res.json();
      renderDrawerState(data.summary);
      if (data.message) window.showStampToast(data.message, data.success ? 'info' : 'error');
    } catch (e) {
      window.showStampToast('Gagal memperbarui Laci Kasir.', 'error');
    }
  }

  async function removeCartItem(variantId) {
    try {
      const res = await fetch('/api/cart/remove', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ variantId, quantity: 0 })
      });
      const data = await res.json();
      renderDrawerState(data.summary);
      window.showStampToast(data.message || 'Item dihapus.', 'info');
    } catch (e) {
      window.showStampToast('Gagal menghapus item.', 'error');
    }
  }

  document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('[data-open-cart]').forEach(el => {
      el.addEventListener('click', (e) => {
        e.preventDefault();
        window.openCartDrawer();
      });
    });

    document.querySelectorAll('[data-close-cart]').forEach(el => {
      el.addEventListener('click', window.closeCartDrawer);
    });

    // Accessible Profile Dropdown Menu
    const dropdowns = document.querySelectorAll('[data-profile-dropdown]');
    function closeAllProfileMenus() {
      dropdowns.forEach(dd => {
        const btn = dd.querySelector('[data-profile-toggle]');
        const menu = dd.querySelector('[data-profile-menu]');
        if (btn) btn.setAttribute('aria-expanded', 'false');
        if (menu) menu.classList.add('hidden');
      });
    }

    dropdowns.forEach(dd => {
      const btn = dd.querySelector('[data-profile-toggle]');
      const menu = dd.querySelector('[data-profile-menu]');
      if (!btn || !menu) return;

      btn.addEventListener('click', (e) => {
        e.stopPropagation();
        const isOpen = !menu.classList.contains('hidden');
        closeAllProfileMenus();
        if (!isOpen) {
          menu.classList.remove('hidden');
          btn.setAttribute('aria-expanded', 'true');
        }
      });
    });

    document.addEventListener('click', (e) => {
      if (!e.target.closest('[data-profile-dropdown]')) {
        closeAllProfileMenus();
      }
    });

    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') {
        closeAllProfileMenus();
      }
    });

    // Hydrate cart badge on page load
    window.refreshCartDrawer();
  });
})();
