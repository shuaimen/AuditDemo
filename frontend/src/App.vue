<template>
  <div>
    <div class="topbar">
      <div class="container">
        <div style="font-weight:700">代工厂评鉴 Demo</div>
        <div>
          <router-link to="/audits" style="margin-right:10px">评鉴单</router-link>
          <router-link to="/reports" style="margin-right:10px">看板</router-link>
          <a href="#" v-if="token" @click.prevent="logout">退出</a>
        </div>
      </div>
    </div>

    <div class="container">
      <router-view />
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'
const token = computed(() => localStorage.getItem('token'))
function logout() {
  localStorage.removeItem('token')
  localStorage.removeItem('role')
  localStorage.removeItem('userId')
  location.href = '/app/login'
}
</script>
