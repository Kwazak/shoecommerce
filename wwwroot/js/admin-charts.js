// Karya Jaya 1978 - Admin Analytics Chart.js Configuration (1970s-1980s Print Palette)
window.renderKaryaJayaAdminCharts = function (payload) {
  if (typeof Chart === 'undefined' || !payload) return;

  Chart.defaults.font.family = "'Courier Prime', monospace";
  Chart.defaults.color = '#231F20';

  const trendCanvas = document.getElementById('revenueTrendChart');
  if (trendCanvas) {
    new Chart(trendCanvas.getContext('2d'), {
      type: 'bar',
      data: {
        labels: payload.trendLabels,
        datasets: [
          {
            label: 'Penjualan Eceran (B2C)',
            data: payload.trendB2C,
            backgroundColor: '#C85A32',
            borderColor: '#231F20',
            borderWidth: 2
          },
          {
            label: 'Penjualan Kodian (B2B)',
            data: payload.trendB2B,
            backgroundColor: '#2E4A3E',
            borderColor: '#231F20',
            borderWidth: 2
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        scales: {
          x: {
            stacked: true,
            grid: { color: 'rgba(35, 31, 32, 0.12)' }
          },
          y: {
            stacked: true,
            beginAtZero: true,
            grid: { color: 'rgba(35, 31, 32, 0.12)' },
            ticks: {
              callback: (v) => 'Rp ' + Number(v).toLocaleString('id-ID')
            }
          }
        },
        plugins: {
          legend: {
            position: 'bottom',
            labels: { boxWidth: 14, padding: 14 }
          }
        }
      }
    });
  }

  const categoryCanvas = document.getElementById('categoryBreakdownChart');
  if (categoryCanvas) {
    new Chart(categoryCanvas.getContext('2d'), {
      type: 'doughnut',
      data: {
        labels: payload.categoryLabels,
        datasets: [{
          data: payload.categoryPairs,
          backgroundColor: ['#4A2E1B', '#C85A32', '#2E4A3E'],
          borderColor: '#231F20',
          borderWidth: 2
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            position: 'bottom',
            labels: { boxWidth: 14, padding: 12 }
          }
        }
      }
    });
  }

  const paymentCanvas = document.getElementById('paymentBreakdownChart');
  if (paymentCanvas) {
    new Chart(paymentCanvas.getContext('2d'), {
      type: 'bar',
      data: {
        labels: payload.paymentLabels,
        datasets: [{
          label: 'Omzet (Rp)',
          data: payload.paymentRevenue,
          backgroundColor: ['#4A2E1B', '#C85A32', '#2E4A3E'],
          borderColor: '#231F20',
          borderWidth: 2
        }]
      },
      options: {
        indexAxis: 'y',
        responsive: true,
        maintainAspectRatio: false,
        scales: {
          x: {
            beginAtZero: true,
            grid: { color: 'rgba(35, 31, 32, 0.12)' },
            ticks: {
              callback: (v) => 'Rp ' + Number(v).toLocaleString('id-ID')
            }
          },
          y: {
            grid: { display: false }
          }
        },
        plugins: {
          legend: { display: false }
        }
      }
    });
  }
};
