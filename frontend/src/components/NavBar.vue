<template>
  <nav class="navbar navbar-expand-lg navbar-dark navbar-ci">
    <div class="container-fluid">
      <a class="navbar-brand fw-semibold" href="#/audits">OEM 代工厂评鉴</a>
      <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#nav" aria-controls="nav" aria-expanded="false">
        <span class="navbar-toggler-icon"></span>
      </button>

      <div class="collapse navbar-collapse" id="nav">
        <ul class="navbar-nav me-auto mb-2 mb-lg-0" v-if="authed">
          <li class="nav-item"><a class="nav-link" href="#/audits">评鉴单</a></li>
          <li class="nav-item"><a class="nav-link" href="#/certificates">证照</a></li>
          <li class="nav-item"><a class="nav-link" href="#/reports">看板</a></li>
        </ul>

        <div class="d-flex" v-if="authed">
          <button class="btn btn-sm btn-outline-light" @click="logout">退出</button>
        </div>
        <div class="d-flex" v-else>
          <a class="btn btn-sm btn-outline-light" href="#/login">登录</a>
        </div>
      </div>
    </div>
  </nav>
</template>

<script setup>
import { computed } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const authed = computed(() => !!localStorage.getItem('token'))

function logout() {
  localStorage.removeItem('token')
  router.push('/login')
}
</script>
